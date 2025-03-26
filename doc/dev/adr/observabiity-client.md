# Observability Client API Design

## APIs

The observability client periodically sends a list of metrics in a single telemetry message. A metric can be one of three types (`Counter`, `Gauge`, `Histogram`), as indicated by the `metric_type` field. The `Metric` structure is defined as follows:

```rust
struct Metric {
  /// Custom labels to associate with the metric
  pub labels: Option<HashMap<String, String>>,

  /// Defines who can access the metric: internal, customer, or regular
  pub metric_scope: MetricScope,

  /// Metric type: Counter, Gauge, Histogram
  pub metric_type: MetricType,

  /// Name of the metric
  pub name: String,

  /// Timestamp of when the metric was recorded
  pub timestamp: Option<DateTime<Utc>>,

  /// Unit of the metric
  pub unit: Option<String>,

  /// Value of the metric
  pub value: f64,

  /// TODO: this needs to be added to the dtdl
  /// Type of operation: Instantiate or Update
  pub operation_type: OperationType,
}
```

The observability client provides APIs to create and manage metrics. The API design is as follows:

```rust
impl ObservabilityClient {    
  /// Creates a new observability client.
  pub fn new() -> ObservabilityClient;

  /// Creates a counter metric.
  pub fn create_counter(
    name: String, 
    initial_value: f64, 
    metric_scope: MetricScope, 
    labels: HashMap<String, String>
  ) -> Counter;

  /// Creates a gauge metric.
  pub fn create_gauge(
    name: String, 
    initial_value: f64, 
    metric_scope: MetricScope, 
    labels: HashMap<String, String>
  ) -> Gauge;

  /// Creates a histogram metric.
  pub fn create_histogram(
    name: String, 
    initial_value: f64, 
    metric_scope: MetricScope, 
    labels: HashMap<String, String>
  ) -> Histogram;
}
```

Each metric type has specific methods for updating its value:

```rust
impl Counter {
  /// Increments the counter by the specified value.
  pub fn increment(value: f64);
}

impl Gauge {        
  /// Records a new value for the gauge.
  pub fn record(value: f64);
}

impl Histogram {
  /// Records a new value for the histogram.
  pub fn record(value: f64);
}  
```

## Internal Logic

- The sweep frequency is determined by the `observability.metric.exportIntervalSeconds` environment variable when the observability client is created.
- When a metric object is created, a `Metric` is instantiated with `OperationType` set to `Instantiate`.
- The observability client tracks updates to each metric between sweeps:
  - **Counter**: Reports the cumulative value at sweep time. For example, if a counter starts at 0 and is incremented ten times by 10, the reported value will be 100.0.
  - **Gauge**: Reports the last recorded value at sweep time.
  - **Histogram**: Queues all updates, as the time of each update is significant. For example, if values 100, 200, and 300 are recorded, three `Metric` entries are added.
- Instantiate behavior:
  - **Counter**: Reflects the updated value if incremented between sweeps.
  - **Gauge**: Reflects the latest recorded value if updated between sweeps.
  - **Histogram**: Sends a `Metric` for each `record` call.

## Errors

- **Duplicate Metric Names**: Two metrics cannot be created with the same name at the same time. For example, it is not allowed to have two different `foo` counters existing simultaneously.
- **Missing Configuration**: If the `observability.metric.exportIntervalSeconds` environment variable is not set, the observability client will fail to initialize.

## Sample Usage

```rust
let observability_client = ObservabilityClient::new(); // Sweep timer set to 3 seconds

// Metrics created at t = 0
let counter_metric = observability_client.create_counter("foo_counter", 0.0, MetricScope::Customer, HashMap::new());
let gauge_metric = observability_client.create_gauge("foo_gauge", 10.0, MetricScope::Internal, HashMap::new());
let histogram_metric = observability_client.create_histogram("foo_histogram", 100.0, MetricScope::Internal, HashMap::new());

counter_metric.increment(10.0); // t = 1
histogram_metric.record(0.0); // t = 2

// Metric Sweep at t = 3
// Sent metrics:
// - Counter: Instantiate, value: 10.0, time: 1
// - Gauge: Instantiate, value: 10.0, time: 0
// - Histogram: Instantiate, value: 100.0, time: 0
// - Histogram: Update, value: 0.0, time: 2

counter_metric.increment(10.0); // t = 4
gauge_metric.record(15.0); // t = 4
histogram_metric.record(200.0); // t = 4
counter_metric.increment(30.0); // t = 5
gauge_metric.record(20.0); // t = 5
histogram_metric.record(300.0); // t = 6

// Metric Sweep at t = 6
// Sent metrics:
// - Counter: Update, value: 50.0, time: 5
// - Gauge: Update, value: 20.0, time: 5
// - Histogram: Update, value: 200.0, time: 4
// - Histogram: Update, value: 300.0, time: 6
```

## Open Questions

- How should `observability.metric.exportIntervalSeconds` be retrieved from `values.yaml`?
- What happens if we fail to send the sweep telemetry?