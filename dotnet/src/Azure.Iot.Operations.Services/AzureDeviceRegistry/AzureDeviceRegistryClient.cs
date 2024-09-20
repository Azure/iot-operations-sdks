using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClient
    {
        string configMapMountPathEnvVar = "CONFIGMAP_MOUNT_PATH";
        string mqTlsCertMountPathEnvVar = "MQ_TLS_CERT_MOUNT_PATH";
        string MqSatMountPathEnvVar = "MQ_SAT_MOUNT_PATH";
        string AepUsernameSecretMountPathEnvVar = "AEP_USERNAME_SECRET_MOUNT_PATH";
        string AepPasswordSecretMountPathEnvVar = "AEP_PASSWORD_SECRET_MOUNT_PATH";
        string AepCertMountPathEnvVar = "AEP_CERT_MOUNT_PATH";

        // Can use this to monitor for changes in the volume mount values
        FileSystemWatcher? fileSystemWatcher;

        public AzureDeviceRegistryClient()
        {
            string configMapMountPath = Environment.GetEnvironmentVariable(configMapMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string mqTlsCertMountPath = Environment.GetEnvironmentVariable(mqTlsCertMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string mqSatMountPath = Environment.GetEnvironmentVariable(MqSatMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string aepUsernameSecretMountPath = Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string aepPasswordSecretMountPath = Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string aepCertMountPath = Environment.GetEnvironmentVariable(AepCertMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");

            string configMapMountPath = File.ReadAllBytes(configMapMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string mqTlsCertMountPath = File.ReadAllBytes(mqTlsCertMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string mqSatMountPath = File.ReadAllBytes(MqSatMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string aepUsernameSecretMountPath = File.ReadAllBytes(AepUsernameSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string aepPasswordSecretMountPath = File.ReadAllBytes(AepPasswordSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            string aepCertMountPath = File.ReadAllBytes(AepCertMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            

        }

        public async Task<int> GetAssetAsync()
        { 
        
        
        }

        public async Task<int> GetAssetCredentialsAsync()
        { 
        
        }

        public async Task ObserveAssetAsync()
        { 
        
        }

        public async Task ObserveAssetCredentialsAsync()
        { 
        
        }
    }
}
