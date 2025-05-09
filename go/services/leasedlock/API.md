<!-- Code generated by gomarkdoc. DO NOT EDIT -->

```go
import "github.com/Azure/iot-operations-sdks/go/services/leasedlock"
```

## Index

- [Variables](<#variables>)
- [type Bytes](<#Bytes>)
- [type Change](<#Change>)
- [type Edit](<#Edit>)
- [type Lease](<#Lease>)
  - [func NewLease\[K, V Bytes\]\(client \*statestore.Client\[K, V\], name K, opt ...Option\) \*Lease\[K, V\]](<#NewLease>)
  - [func \(l \*Lease\[K, V\]\) Acquire\(ctx context.Context, duration time.Duration, opt ...Option\) \(bool, error\)](<#Lease[K, V].Acquire>)
  - [func \(l \*Lease\[K, V\]\) Holder\(ctx context.Context, opt ...Option\) \(string, bool, error\)](<#Lease[K, V].Holder>)
  - [func \(l \*Lease\[K, V\]\) Observe\(\) \(\<\-chan Change, func\(\)\)](<#Lease[K, V].Observe>)
  - [func \(l \*Lease\[K, V\]\) ObserveStart\(ctx context.Context, opt ...Option\) error](<#Lease[K, V].ObserveStart>)
  - [func \(l \*Lease\[K, V\]\) ObserveStop\(ctx context.Context, opt ...Option\) error](<#Lease[K, V].ObserveStop>)
  - [func \(l \*Lease\[K, V\]\) Release\(ctx context.Context, opt ...Option\) error](<#Lease[K, V].Release>)
  - [func \(l \*Lease\[K, V\]\) Token\(ctx context.Context\) \(hlc.HybridLogicalClock, error\)](<#Lease[K, V].Token>)
- [type Lock](<#Lock>)
  - [func NewLock\[K, V Bytes\]\(client \*statestore.Client\[K, V\], name K, opt ...Option\) Lock\[K, V\]](<#NewLock>)
  - [func \(l Lock\[K, V\]\) Edit\(ctx context.Context, key K, duration time.Duration, edit Edit\[V\], opt ...Option\) error](<#Lock[K, V].Edit>)
  - [func \(l Lock\[K, V\]\) Lock\(ctx context.Context, duration time.Duration, opt ...Option\) error](<#Lock[K, V].Lock>)
  - [func \(l Lock\[K, V\]\) Unlock\(ctx context.Context, opt ...Option\) error](<#Lock[K, V].Unlock>)
- [type Option](<#Option>)
- [type Options](<#Options>)
  - [func \(o \*Options\) Apply\(opts \[\]Option, rest ...Option\)](<#Options.Apply>)
- [type WithRenew](<#WithRenew>)
- [type WithSessionID](<#WithSessionID>)
- [type WithTimeout](<#WithTimeout>)


## Variables

<a name="ErrNoLease"></a>

```go
var (
    // ErrNoLease is used in absence of other errors to indicate that the lease
    // has not been acquired.
    ErrNoLease = errors.New("lease not acquired")

    // ErrRenewing indicates that renew was specified on a lease that is already
    // renewing.
    ErrRenewing = errors.New("lease already renewing")
)
```

<a name="Bytes"></a>
## type [Bytes](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L16>)

Bytes represents generic byte data.

```go
type Bytes = statestore.Bytes
```

<a name="Change"></a>
## type [Change](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L31-L34>)

Change represents an observed change in the lease holder.

```go
type Change struct {
    Held   bool
    Holder string
}
```

<a name="Edit"></a>
## type [Edit](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lock.go#L22>)

Edit provides a callback to edit a value under protection of a lock. Given the current value when the lock is acquired and whether that value was present, it should return the updated value and whether the new value should be set \(true\) or deleted \(false\).

```go
type Edit[V Bytes] = func(context.Context, V, bool) (V, bool, error)
```

<a name="Lease"></a>
## type [Lease](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L19-L28>)

Lease provides a distributed lease based on an underlying state store.

```go
type Lease[K, V Bytes] struct {
    Name      K
    SessionID string
    // contains filtered or unexported fields
}
```

<a name="NewLease"></a>
### func [NewLease](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L56-L60>)

```go
func NewLease[K, V Bytes](client *statestore.Client[K, V], name K, opt ...Option) *Lease[K, V]
```

NewLease creates a new distributed lease from an underlying state store client and a lease name.

<a name="Lease[K, V].Acquire"></a>
### func \(\*Lease\[K, V\]\) [Acquire](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L105-L109>)

```go
func (l *Lease[K, V]) Acquire(ctx context.Context, duration time.Duration, opt ...Option) (bool, error)
```

Acquire performs a single attempt to acquire the lease, returning whether it was successful. If the lease was already held by another client, this will return false with no error.

<a name="Lease[K, V].Holder"></a>
### func \(\*Lease\[K, V\]\) [Holder](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L214-L217>)

```go
func (l *Lease[K, V]) Holder(ctx context.Context, opt ...Option) (string, bool, error)
```

Holder gets the current holder of the lease and an indicator of whether the lease is currently held.

<a name="Lease[K, V].Observe"></a>
### func \(\*Lease\[K, V\]\) [Observe](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L252>)

```go
func (l *Lease[K, V]) Observe() (<-chan Change, func())
```

Observe requests a lease holder change notification channel for this lease. It returns the channel and a function to remove and close that channel. Note that ObserveStart must be called to actually start observing \(though changes may be received on this channel if ObserveStart had already been called previously\).

<a name="Lease[K, V].ObserveStart"></a>
### func \(\*Lease\[K, V\]\) [ObserveStart](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L230>)

```go
func (l *Lease[K, V]) ObserveStart(ctx context.Context, opt ...Option) error
```

ObserveStart initializes observation of lease holder changes. It should be paired with a call to ObserveStop.

<a name="Lease[K, V].ObserveStop"></a>
### func \(\*Lease\[K, V\]\) [ObserveStop](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L240>)

```go
func (l *Lease[K, V]) ObserveStop(ctx context.Context, opt ...Option) error
```

ObserveStop terminates observation of lease holder changes. It should only be called once per successfull call to ObserveStart \(but may be retried in case of failure\).

<a name="Lease[K, V].Release"></a>
### func \(\*Lease\[K, V\]\) [Release](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L181-L184>)

```go
func (l *Lease[K, V]) Release(ctx context.Context, opt ...Option) error
```

Release the lease.

<a name="Lease[K, V].Token"></a>
### func \(\*Lease\[K, V\]\) [Token](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lease.go#L91-L93>)

```go
func (l *Lease[K, V]) Token(ctx context.Context) (hlc.HybridLogicalClock, error)
```

Token returns the current fencing token value or the error that caused the lease to fail. Note that this function will block if the lease is currently renewing and can be cancelled using its context.

<a name="Lock"></a>
## type [Lock](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lock.go#L16>)

Lock provides a distributed mutex\-like lock based on an underlying state store.

```go
type Lock[K, V Bytes] struct {
    // contains filtered or unexported fields
}
```

<a name="NewLock"></a>
### func [NewLock](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lock.go#L27-L31>)

```go
func NewLock[K, V Bytes](client *statestore.Client[K, V], name K, opt ...Option) Lock[K, V]
```

NewLock creates a new distributed lock from an underlying state store client and a lock name.

<a name="Lock[K, V].Edit"></a>
### func \(Lock\[K, V\]\) [Edit](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lock.go#L104-L110>)

```go
func (l Lock[K, V]) Edit(ctx context.Context, key K, duration time.Duration, edit Edit[V], opt ...Option) error
```

Edit a key under the protection of this lock.

<a name="Lock[K, V].Lock"></a>
### func \(Lock\[K, V\]\) [Lock](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lock.go#L38-L42>)

```go
func (l Lock[K, V]) Lock(ctx context.Context, duration time.Duration, opt ...Option) error
```

Lock the lock object, blocking until locked or the request fails. Note that cancelling the context passed to this method will prevent the underlying notification from stopping; it is recommended to use WithTimeout instead.

<a name="Lock[K, V].Unlock"></a>
### func \(Lock\[K, V\]\) [Unlock](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/lock.go#L96-L99>)

```go
func (l Lock[K, V]) Unlock(ctx context.Context, opt ...Option) error
```

Unlock the lock object.

<a name="Option"></a>
## type [Option](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/options.go#L14>)

Option represents a single option for the lock requests.

```go
type Option interface {
    // contains filtered or unexported methods
}
```

<a name="Options"></a>
## type [Options](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/options.go#L17-L21>)

Options are the resolved options for the lock requests.

```go
type Options struct {
    Timeout   time.Duration
    SessionID string
    Renew     time.Duration
}
```

<a name="Options.Apply"></a>
### func \(\*Options\) [Apply](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/options.go#L36-L39>)

```go
func (o *Options) Apply(opts []Option, rest ...Option)
```

Apply resolves the provided list of options.

<a name="WithRenew"></a>
## type [WithRenew](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/options.go#L32>)

WithRenew adds a renew interval to the lock; the lock will continuously re\-acquire itself at this interval until it fails or is terminated.

```go
type WithRenew time.Duration
```

<a name="WithSessionID"></a>
## type [WithSessionID](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/options.go#L28>)

WithSessionID adds an optional session ID suffix to the lock holder to allow distinct locks on the same key with the same MQTT client.

```go
type WithSessionID string
```

<a name="WithTimeout"></a>
## type [WithTimeout](<https://github.com/Azure/iot-operations-sdks/blob/main/go/services/leasedlock/options.go#L24>)

WithTimeout adds a timeout to the request \(with second precision\).

```go
type WithTimeout time.Duration
```

Generated by [gomarkdoc](<https://github.com/princjef/gomarkdoc>)
