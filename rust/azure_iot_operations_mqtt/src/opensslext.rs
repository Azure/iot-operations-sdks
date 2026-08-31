// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Extensions to `openssl` for kernel TLS.

pub mod ssl {
    use hkdf::Hkdf;
    use hmac::{
        Hmac, Mac,
        digest::{FixedOutputReset, OutputSizeUser, crypto_common::KeyInit},
    };
    use openssl::{
        nid::Nid,
        ssl::{SslCipherRef, SslRef, SslVersion},
    };
    use sha2::{Sha256, Sha384};

    pub struct ExtractedSecrets {
        pub client: (u64, ConnectionTrafficSecrets),
        pub server: (u64, ConnectionTrafficSecrets),
    }

    pub enum ConnectionTrafficSecrets {
        Aes128Gcm {
            key: [u8; libc::TLS_CIPHER_AES_GCM_128_KEY_SIZE],
            salt: [u8; libc::TLS_CIPHER_AES_GCM_128_SALT_SIZE],
            iv: [u8; libc::TLS_CIPHER_AES_GCM_128_IV_SIZE],
        },

        Aes256Gcm {
            key: [u8; libc::TLS_CIPHER_AES_GCM_256_KEY_SIZE],
            salt: [u8; libc::TLS_CIPHER_AES_GCM_256_SALT_SIZE],
            iv: [u8; libc::TLS_CIPHER_AES_GCM_256_IV_SIZE],
        },

        Chacha20Poly1305 {
            key: [u8; libc::TLS_CIPHER_CHACHA20_POLY1305_KEY_SIZE],
            salt: [u8; libc::TLS_CIPHER_CHACHA20_POLY1305_SALT_SIZE],
            iv: [u8; libc::TLS_CIPHER_CHACHA20_POLY1305_IV_SIZE],
        },
    }

    /// Derives the connection traffic secrets from the given TLS context.
    pub fn extract_secrets(
        ssl: &SslRef,
        client_traffic_secret: Option<&[u8]>,
        server_traffic_secret: Option<&[u8]>,
    ) -> Result<ExtractedSecrets, &'static str> {
        let protocol_version = ssl.version2().expect("handshake has been completed");

        match protocol_version {
            SslVersion::TLS1_2 => Ok(tls12_extract_secrets(ssl)),

            SslVersion::TLS1_3 => {
                let cipher = ssl.current_cipher().expect("handshake has been completed");
                let client = (
                    // HACK: OpenSSL doesn't expose the record sequence number,
                    // but we know that we don't send any post-handshake messages,
                    // so we know it's 0.
                    0,
                    tls13_extract_secrets(
                        cipher,
                        client_traffic_secret
                            .ok_or("TLS 1.3 handshake did not generate client traffic secret")?,
                    )?,
                );
                let server = (
                    // HACK: OpenSSL doesn't expose the record sequence number,
                    // but we know that we haven't processed any post-handshake messages from it yet,
                    // so we know it's 0.
                    0,
                    tls13_extract_secrets(
                        cipher,
                        server_traffic_secret
                            .ok_or("TLS 1.3 handshake did not generate server traffic secret")?,
                    )?,
                );
                Ok(ExtractedSecrets { client, server })
            }

            protocol_version => unreachable!(
                "expected OpenSSL to negotiate TLS 1.2 or 1.3 because of prepare_for_ktls but it negotiated {protocol_version:?}"
            ),
        }
    }

    fn tls12_extract_secrets(ssl: &SslRef) -> ExtractedSecrets {
        // Ref: https://datatracker.ietf.org/doc/html/rfc5246#section-6.1
        const MASTER_SECRET_LEN: usize = 48;
        const CLIENT_RANDOM_LEN: usize = 32;
        const SERVER_RANDOM_LEN: usize = 32;

        // Ref: https://datatracker.ietf.org/doc/html/rfc5246#section-6.3

        #[repr(C)]
        struct KeyBlock<
            const WRITE_KEY_LENGTH: usize,
            const FIXED_IV_LENGTH: usize,
            const RECORD_IV_LENGTH: usize,
        > {
            client_write_key: [u8; WRITE_KEY_LENGTH],
            server_write_key: [u8; WRITE_KEY_LENGTH],
            client_fixed_iv: [u8; FIXED_IV_LENGTH],
            server_fixed_iv: [u8; FIXED_IV_LENGTH],
            record_iv: [u8; RECORD_IV_LENGTH],
        }

        impl<
            const WRITE_KEY_LENGTH: usize,
            const FIXED_IV_LENGTH: usize,
            const RECORD_IV_LENGTH: usize,
        > Default for KeyBlock<WRITE_KEY_LENGTH, FIXED_IV_LENGTH, RECORD_IV_LENGTH>
        where
            [u8; WRITE_KEY_LENGTH]: Default,
            [u8; FIXED_IV_LENGTH]: Default,
            [u8; RECORD_IV_LENGTH]: Default,
        {
            fn default() -> Self {
                Self {
                    client_write_key: Default::default(),
                    server_write_key: Default::default(),
                    client_fixed_iv: Default::default(),
                    server_fixed_iv: Default::default(),
                    record_iv: Default::default(),
                }
            }
        }

        impl<
            const WRITE_KEY_LENGTH: usize,
            const FIXED_IV_LENGTH: usize,
            const RECORD_IV_LENGTH: usize,
        > AsMut<[u8]> for KeyBlock<WRITE_KEY_LENGTH, FIXED_IV_LENGTH, RECORD_IV_LENGTH>
        {
            fn as_mut(&mut self) -> &mut [u8] {
                // SAFETY: KeyBlock is repr(C) and the array lengths are such that there is no padding between them.
                unsafe {
                    std::slice::from_raw_parts_mut(
                        self.client_write_key.as_mut_ptr(),
                        self.client_write_key.len()
                            + self.server_write_key.len()
                            + self.client_fixed_iv.len()
                            + self.server_fixed_iv.len()
                            + self.record_iv.len(),
                    )
                }
            }
        }

        const PRF_LABEL: &[u8] = b"key expansion";

        let master_secret = {
            let session = ssl.session().expect("handshake has been completed");
            let mut master_secret = [0_u8; MASTER_SECRET_LEN];
            let master_secret_len = session.master_key(&mut []);
            assert_eq!(master_secret.len(), master_secret_len);
            let master_secret_len = session.master_key(&mut master_secret);
            assert_eq!(master_secret.len(), master_secret_len);
            master_secret
        };

        let client_random = {
            let mut client_random = [0_u8; CLIENT_RANDOM_LEN];
            let client_random_len = ssl.client_random(&mut []);
            assert_eq!(client_random.len(), client_random_len);
            let client_random_len = ssl.client_random(&mut client_random);
            assert_eq!(client_random.len(), client_random_len);
            client_random
        };

        let server_random = {
            let mut server_random = [0_u8; SERVER_RANDOM_LEN];
            let server_random_len = ssl.server_random(&mut []);
            assert_eq!(server_random.len(), server_random_len);
            let server_random_len = ssl.server_random(&mut server_random);
            assert_eq!(server_random.len(), server_random_len);
            server_random
        };

        let cipher = ssl.current_cipher().expect("handshake has been completed");
        match (
            cipher.cipher_nid().expect("handshake has been completed"),
            cipher
                .handshake_digest()
                .expect("handshake has been completed")
                .type_(),
        ) {
            (Nid::AES_128_GCM, Nid::SHA256) => {
                // Ref: https://datatracker.ietf.org/doc/html/rfc5246#section-6.2.3.3

                let mut key_block = KeyBlock::<
                    { libc::TLS_CIPHER_AES_GCM_128_KEY_SIZE },
                    { libc::TLS_CIPHER_AES_GCM_128_SALT_SIZE },
                    { libc::TLS_CIPHER_AES_GCM_128_IV_SIZE },
                >::default();
                prf::<Hmac<Sha256>>(
                    key_block.as_mut(),
                    &master_secret,
                    PRF_LABEL,
                    [&server_random, &client_random],
                );
                let KeyBlock {
                    client_write_key,
                    server_write_key,
                    client_fixed_iv,
                    server_fixed_iv,
                    record_iv,
                } = key_block;

                let client = (
                    1,
                    ConnectionTrafficSecrets::Aes128Gcm {
                        key: client_write_key,
                        salt: client_fixed_iv,
                        iv: record_iv,
                    },
                );

                let server = (
                    1,
                    ConnectionTrafficSecrets::Aes128Gcm {
                        key: server_write_key,
                        salt: server_fixed_iv,
                        iv: record_iv,
                    },
                );

                ExtractedSecrets { client, server }
            }

            (Nid::AES_256_GCM, Nid::SHA384) => {
                // Ref: https://datatracker.ietf.org/doc/html/rfc5246#section-6.2.3.3

                let mut key_block = KeyBlock::<
                    { libc::TLS_CIPHER_AES_GCM_256_KEY_SIZE },
                    { libc::TLS_CIPHER_AES_GCM_256_SALT_SIZE },
                    { libc::TLS_CIPHER_AES_GCM_256_IV_SIZE },
                >::default();
                prf::<Hmac<Sha384>>(
                    key_block.as_mut(),
                    &master_secret,
                    PRF_LABEL,
                    [&server_random, &client_random],
                );
                let KeyBlock {
                    client_write_key,
                    server_write_key,
                    client_fixed_iv,
                    server_fixed_iv,
                    record_iv,
                } = key_block;

                let client = (
                    1,
                    ConnectionTrafficSecrets::Aes256Gcm {
                        key: client_write_key,
                        salt: client_fixed_iv,
                        iv: record_iv,
                    },
                );

                let server = (
                    1,
                    ConnectionTrafficSecrets::Aes256Gcm {
                        key: server_write_key,
                        salt: server_fixed_iv,
                        iv: record_iv,
                    },
                );

                ExtractedSecrets { client, server }
            }

            (Nid::CHACHA20_POLY1305, Nid::SHA256) => {
                // Ref: https://datatracker.ietf.org/doc/html/rfc7905#section-2

                let mut key_block = KeyBlock::<
                    { libc::TLS_CIPHER_CHACHA20_POLY1305_KEY_SIZE },
                    { libc::TLS_CIPHER_CHACHA20_POLY1305_IV_SIZE },
                    { libc::TLS_CIPHER_CHACHA20_POLY1305_SALT_SIZE },
                >::default();
                prf::<Hmac<Sha256>>(
                    key_block.as_mut(),
                    &master_secret,
                    PRF_LABEL,
                    [&server_random, &client_random],
                );
                let KeyBlock {
                    client_write_key,
                    server_write_key,
                    client_fixed_iv,
                    server_fixed_iv,
                    record_iv: [],
                } = key_block;

                let client = (
                    1,
                    ConnectionTrafficSecrets::Chacha20Poly1305 {
                        key: client_write_key,
                        salt: [],
                        iv: client_fixed_iv,
                    },
                );

                let server = (
                    1,
                    ConnectionTrafficSecrets::Chacha20Poly1305 {
                        key: server_write_key,
                        salt: [],
                        iv: server_fixed_iv,
                    },
                );

                ExtractedSecrets { client, server }
            }

            (cipher_nid, digest_nid) => {
                unreachable!(
                    "expected OpenSSL to negotiate a ciphersuite listed in prepare_for_ktls but it negotiated {cipher_nid:?} {digest_nid:?} {cipher:?}"
                );
            }
        }
    }

    // Ref: https://datatracker.ietf.org/doc/html/rfc5246#section-5
    //
    // `seed` is taken as two slices and `P_HASH` is inlined into this,
    // so that we can use label, client random and server random without having to
    // create a new buffer for their concatenation.
    fn prf<TMac>(result: &mut [u8], secret: &[u8], label: &[u8], seed: [&[u8]; 2])
    where
        TMac: FixedOutputReset + KeyInit + Mac,
    {
        let mut hmac: TMac = Mac::new_from_slice(secret).expect("Hmac accepts any key length");

        // A(1)
        //
        // It's convenient to start from A(1) rather than A(0) so that the type of `a`
        // is always `GenericArray<<TMac as OutputSizeUser>::output_size()>`
        Mac::update(&mut hmac, label);
        Mac::update(&mut hmac, seed[0]);
        Mac::update(&mut hmac, seed[1]);
        let mut a = hmac.finalize_reset().into_bytes();

        for chunk in result.chunks_mut(<TMac as OutputSizeUser>::output_size()) {
            Mac::update(&mut hmac, &a);
            Mac::update(&mut hmac, label);
            Mac::update(&mut hmac, seed[0]);
            Mac::update(&mut hmac, seed[1]);
            let prf_chunk = hmac.finalize_reset().into_bytes();
            // All `chunk`s should be the same length as `prf_chunk`,
            // except the last `chunk` which might be smaller.
            chunk.copy_from_slice(&prf_chunk[..chunk.len()]);

            Mac::update(&mut hmac, &a);
            a = hmac.finalize_reset().into_bytes();
        }
    }

    fn tls13_extract_secrets(
        cipher: &SslCipherRef,
        traffic_secret: &[u8],
    ) -> Result<ConnectionTrafficSecrets, &'static str> {
        // Ref: https://datatracker.ietf.org/doc/html/rfc8446#section-7.3

        #[repr(C)]
        struct Iv<const SALT_LENGTH: usize, const IV_LENGTH: usize> {
            salt: [u8; SALT_LENGTH],
            iv: [u8; IV_LENGTH],
        }

        impl<const SALT_LENGTH: usize, const IV_LENGTH: usize> Default for Iv<SALT_LENGTH, IV_LENGTH>
        where
            [u8; SALT_LENGTH]: Default,
            [u8; IV_LENGTH]: Default,
        {
            fn default() -> Self {
                Self {
                    salt: Default::default(),
                    iv: Default::default(),
                }
            }
        }

        impl<const SALT_LENGTH: usize, const IV_LENGTH: usize> AsMut<[u8]> for Iv<SALT_LENGTH, IV_LENGTH> {
            fn as_mut(&mut self) -> &mut [u8] {
                // SAFETY: Iv is repr(C) and the array lengths are such that there is no padding between them.
                unsafe {
                    std::slice::from_raw_parts_mut(
                        self.salt.as_mut_ptr(),
                        self.salt.len() + self.iv.len(),
                    )
                }
            }
        }

        match (
            cipher.cipher_nid().expect("handshake has been completed"),
            cipher
                .handshake_digest()
                .expect("handshake has been completed")
                .type_(),
        ) {
            (Nid::AES_128_GCM, Nid::SHA256) => {
                let hkdf = Hkdf::<Sha256>::from_prk(traffic_secret)
                    .map_err(|_| "traffic secret is not the right length for HKDF-SHA256 PRK")?;

                let mut key = [0_u8; libc::TLS_CIPHER_AES_GCM_128_KEY_SIZE];
                hkdf.expand(HKDF_LABEL_AES_128_GCM_KEY, &mut key)
                    .expect("slice length is valid");

                let mut iv = Iv::<
                    { libc::TLS_CIPHER_AES_GCM_128_SALT_SIZE },
                    { libc::TLS_CIPHER_AES_GCM_128_IV_SIZE },
                >::default();
                hkdf.expand(HKDF_LABEL_AES_128_GCM_IV, iv.as_mut())
                    .expect("slice length is valid");
                let Iv { salt, iv } = iv;

                Ok(ConnectionTrafficSecrets::Aes128Gcm { key, salt, iv })
            }

            (Nid::AES_256_GCM, Nid::SHA384) => {
                let hkdf = Hkdf::<Sha384>::from_prk(traffic_secret)
                    .map_err(|_| "traffic secret is not the right length for HKDF-SHA384 PRK")?;

                let mut key = [0_u8; libc::TLS_CIPHER_AES_GCM_256_KEY_SIZE];
                hkdf.expand(HKDF_LABEL_AES_256_GCM_KEY, &mut key)
                    .expect("slice length is valid");

                let mut iv = Iv::<
                    { libc::TLS_CIPHER_AES_GCM_256_SALT_SIZE },
                    { libc::TLS_CIPHER_AES_GCM_256_IV_SIZE },
                >::default();
                hkdf.expand(HKDF_LABEL_AES_256_GCM_IV, iv.as_mut())
                    .expect("slice length is valid");
                let Iv { salt, iv } = iv;

                Ok(ConnectionTrafficSecrets::Aes256Gcm { key, salt, iv })
            }

            (Nid::CHACHA20_POLY1305, Nid::SHA256) => {
                let hkdf = Hkdf::<Sha256>::from_prk(traffic_secret)
                    .map_err(|_| "traffic secret is not the right length for HKDF-SHA256 PRK")?;

                let mut key = [0_u8; libc::TLS_CIPHER_CHACHA20_POLY1305_KEY_SIZE];
                hkdf.expand(HKDF_LABEL_CHACHA20_POLY1305_KEY, &mut key)
                    .expect("slice length is valid");

                let mut iv = Iv::<
                    { libc::TLS_CIPHER_CHACHA20_POLY1305_SALT_SIZE },
                    { libc::TLS_CIPHER_CHACHA20_POLY1305_IV_SIZE },
                >::default();
                hkdf.expand(HKDF_LABEL_CHACHA20_POLY1305_IV, iv.as_mut())
                    .expect("slice length is valid");
                let Iv { salt, iv } = iv;

                Ok(ConnectionTrafficSecrets::Chacha20Poly1305 { key, salt, iv })
            }

            (cipher_nid, digest_nid) => {
                unreachable!(
                    "expected OpenSSL to negotiate a ciphersuite listed in prepare_for_ktls but it negotiated {cipher_nid:?} {digest_nid:?} {cipher:?}"
                );
            }
        }
    }

    // See `openssl_hardcoded_hkdf_labels` test.
    // TODO(rustup): If it ever becomes possible to run `runtime_impl` in a const context,
    // then these can be generated using the same calls that that test makes instead.
    const HKDF_LABEL_AES_128_GCM_KEY: &[u8] = b"\x00\x10\x09tls13 key\x00";
    const HKDF_LABEL_AES_128_GCM_IV: &[u8] = b"\x00\x0c\x08tls13 iv\x00";
    const HKDF_LABEL_AES_256_GCM_KEY: &[u8] = b"\x00\x20\x09tls13 key\x00";
    const HKDF_LABEL_AES_256_GCM_IV: &[u8] = b"\x00\x0c\x08tls13 iv\x00";
    const HKDF_LABEL_CHACHA20_POLY1305_KEY: &[u8] = b"\x00\x20\x09tls13 key\x00";
    const HKDF_LABEL_CHACHA20_POLY1305_IV: &[u8] = b"\x00\x0c\x08tls13 iv\x00";

    #[cfg(test)]
    mod tests {
        use hmac::Hmac;
        use sha2::{Sha256, Sha384};

        #[test]
        fn openssl_hardcoded_hkdf_labels() {
            // Ref:
            //
            // - https://datatracker.ietf.org/doc/html/rfc8446#section-7.1
            // - https://datatracker.ietf.org/doc/html/rfc8446#section-7.3

            fn runtime_impl(label: &[u8], context: &[u8], length: usize) -> Vec<u8> {
                let mut result = vec![];
                result.extend_from_slice(&u16::try_from(length).unwrap().to_be_bytes());
                result.extend_from_slice(
                    &u8::try_from("tls13 ".len() + label.len())
                        .unwrap()
                        .to_be_bytes(),
                );
                result.extend_from_slice(b"tls13 ");
                result.extend_from_slice(label);
                result.extend_from_slice(&u8::try_from(context.len()).unwrap().to_be_bytes());
                result.extend_from_slice(context);
                result
            }

            let expected_label_aes_128_gcm_key =
                runtime_impl(b"key", b"", libc::TLS_CIPHER_AES_GCM_128_KEY_SIZE);
            assert_eq!(
                expected_label_aes_128_gcm_key,
                super::HKDF_LABEL_AES_128_GCM_KEY,
            );

            let expected_label_aes_128_gcm_iv = runtime_impl(
                b"iv",
                b"",
                libc::TLS_CIPHER_AES_GCM_128_SALT_SIZE + libc::TLS_CIPHER_AES_GCM_128_IV_SIZE,
            );
            assert_eq!(
                expected_label_aes_128_gcm_iv,
                super::HKDF_LABEL_AES_128_GCM_IV,
            );

            let expected_label_aes_256_gcm_key =
                runtime_impl(b"key", b"", libc::TLS_CIPHER_AES_GCM_256_KEY_SIZE);
            assert_eq!(
                expected_label_aes_256_gcm_key,
                super::HKDF_LABEL_AES_256_GCM_KEY,
            );

            let expected_label_aes_256_gcm_iv = runtime_impl(
                b"iv",
                b"",
                libc::TLS_CIPHER_AES_GCM_256_SALT_SIZE + libc::TLS_CIPHER_AES_GCM_256_IV_SIZE,
            );
            assert_eq!(
                expected_label_aes_256_gcm_iv,
                super::HKDF_LABEL_AES_256_GCM_IV,
            );

            let expected_label_chacha20_poly1305_key =
                runtime_impl(b"key", b"", libc::TLS_CIPHER_CHACHA20_POLY1305_KEY_SIZE);
            assert_eq!(
                expected_label_chacha20_poly1305_key,
                super::HKDF_LABEL_CHACHA20_POLY1305_KEY,
            );

            let expected_label_chacha20_poly1305_iv = runtime_impl(
                b"iv",
                b"",
                libc::TLS_CIPHER_CHACHA20_POLY1305_SALT_SIZE
                    + libc::TLS_CIPHER_CHACHA20_POLY1305_IV_SIZE,
            );
            assert_eq!(
                expected_label_chacha20_poly1305_iv,
                super::HKDF_LABEL_CHACHA20_POLY1305_IV,
            );
        }

        #[test]
        fn prf_sha256() {
            // Ref: https://mailarchive.ietf.org/arch/msg/tls/fzVCzk-z3FShgGJ6DOXqM1ydxms/

            const SECRET: &[u8] =
                b"\x9b\xbe\x43\x6b\xa9\x40\xf0\x17\xb1\x76\x52\x84\x9a\x71\xdb\x35";
            const SEED: &[u8] = b"\xa0\xba\x9f\x93\x6c\xda\x31\x18\x27\xa6\xf7\x96\xff\xd5\x19\x8c";
            const LABEL: &[u8] = b"test label";
            const EXPECTED_OUTPUT: &[u8] = b"\xe3\xf2\x29\xba\x72\x7b\xe1\x7b\x8d\x12\x26\x20\x55\x7c\xd4\x53\xc2\xaa\xb2\x1d\x07\xc3\xd4\x95\x32\x9b\x52\xd4\xe6\x1e\xdb\x5a\x6b\x30\x17\x91\xe9\x0d\x35\xc9\xc9\xa4\x6b\x4e\x14\xba\xf9\xaf\x0f\xa0\x22\xf7\x07\x7d\xef\x17\xab\xfd\x37\x97\xc0\x56\x4b\xab\x4f\xbc\x91\x66\x6e\x9d\xef\x9b\x97\xfc\xe3\x4f\x79\x67\x89\xba\xa4\x80\x82\xd1\x22\xee\x42\xc5\xa7\x2e\x5a\x51\x10\xff\xf7\x01\x87\x34\x7b\x66";

            let mut actual_output = [0_u8; EXPECTED_OUTPUT.len()];
            super::prf::<Hmac<Sha256>>(&mut actual_output, SECRET, LABEL, [SEED, b""]);
            assert_eq!(actual_output, *EXPECTED_OUTPUT);
        }

        #[test]
        fn prf_sha384() {
            // Ref: https://mailarchive.ietf.org/arch/msg/tls/fzVCzk-z3FShgGJ6DOXqM1ydxms/

            const SECRET: &[u8] =
                b"\xb8\x0b\x73\x3d\x6c\xee\xfc\xdc\x71\x56\x6e\xa4\x8e\x55\x67\xdf";
            const SEED: &[u8] = b"\xcd\x66\x5c\xf6\xa8\x44\x7d\xd6\xff\x8b\x27\x55\x5e\xdb\x74\x65";
            const LABEL: &[u8] = b"test label";
            const EXPECTED_OUTPUT: &[u8] = b"\x7b\x0c\x18\xe9\xce\xd4\x10\xed\x18\x04\xf2\xcf\xa3\x4a\x33\x6a\x1c\x14\xdf\xfb\x49\x00\xbb\x5f\xd7\x94\x21\x07\xe8\x1c\x83\xcd\xe9\xca\x0f\xaa\x60\xbe\x9f\xe3\x4f\x82\xb1\x23\x3c\x91\x46\xa0\xe5\x34\xcb\x40\x0f\xed\x27\x00\x88\x4f\x9d\xc2\x36\xf8\x0e\xdd\x8b\xfa\x96\x11\x44\xc9\xe8\xd7\x92\xec\xa7\x22\xa7\xb3\x2f\xc3\xd4\x16\xd4\x73\xeb\xc2\xc5\xfd\x4a\xbf\xda\xd0\x5d\x91\x84\x25\x9b\x5b\xf8\xcd\x4d\x90\xfa\x0d\x31\xe2\xde\xc4\x79\xe4\xf1\xa2\x60\x66\xf2\xee\xa9\xa6\x92\x36\xa3\xe5\x26\x55\xc9\xe9\xae\xe6\x91\xc8\xf3\xa2\x68\x54\x30\x8d\x5e\xaa\x3b\xe8\x5e\x09\x90\x70\x3d\x73\xe5\x6f";

            let mut actual_output = [0_u8; EXPECTED_OUTPUT.len()];
            super::prf::<Hmac<Sha384>>(&mut actual_output, SECRET, LABEL, [SEED, b""]);
            assert_eq!(actual_output, *EXPECTED_OUTPUT);
        }
    }
}
