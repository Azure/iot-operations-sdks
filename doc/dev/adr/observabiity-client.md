# Observability Client API Design

## API:

``` Rust

struct Client {
  // The single telemetry sender for all telemetry
  telemetry_sender: TelemetrySender,

  // Queued telemetry
  counter_telemetry_queue: List<Counter>
  metric_telemetry_queue: List<Metric> 
}

impl Client {    
    // sweeper_timer sets the timer to collect all telemetry and send it    
    pub fn new(sweeper_timer: &Duration) -> Client    

    pub fn create_counter(name: string, initial_value: Value, metric_scope: MetricScope, labels: Map<String, String>) -> Counter

    // Metric Types: 

    pub fn create_gauge(name: string, initial_value: Value, metric_scope: MetricScope, labels: Map<String, String>) -> Gauge

    pub fn create_histogram(name: string, initial_value: Value, metric_scope: MetricScope, labels: Map<String, String>) -> Histogram

    // Private function called every sweeper_timer duration to drain counter and metric queues and send them
    fn send_telemetry()
}

struct Counter { /*... follows Counter definition in DTDL */ }

struct Metric { /*... follows Metric definition in DTDL */ }

impl Counter {
    pub fn increment(value: double, unit: string) {
      // Creates a Counter with a timestamp added at the moment `increment` is called
      // Queues Counter
    }
}

impl Gauge {        
    pub fn record(value: double, unit: string) {
      // Creates a Metric of type Gauge with a timestamp added at the moment `record` is called
      // Queues Metric
    }
}

impl Histogram {
    pub fn record(value: double, unit: string) {
      // Creates a Metric of type Histogram with a timestamp added at the moment `record` is called
      // Queues Metric
    }
}  
```

## Questions:

- The above assumes aggregation occurs at this client level, will both Counter and Metric types be aggregated together?
  - The dtdl only allows for one telemetry message to be sent at a time with both one Counter and one Metric piece of telemetry. Are we going to send a batch of individual telemetry messages containing the data or should the dtdl change to allow aggregation?
- Is counter going to be treated differently than metric? They contain the same data except for the Metric type.
  - If not, the `increment` API should have different arguments to reflect a `Counter`: `increment()` and `reset()`
- Should the API for each metric type change? Gauge and Histogram make sense to have the same as it is only reporting a number.
- Will errors be scraped from the terminal?