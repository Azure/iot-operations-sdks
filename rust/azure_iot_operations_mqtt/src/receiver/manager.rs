use std::sync::Arc;

use tokio::sync::mpsc::UnboundedReceiver;
use tokio::sync::Notify;

use crate::control_packet::Publish;
use crate::interface::{CompletionToken, MqttAck};

pub struct AckToken {
    //reference to the ack_tracker?
    // notify field
    // result field
    notify: Arc<Notify>,
    result: Option<CompletionToken>
}

pub struct InboundPublishManager<A>
where A: MqttAck
{
    acker: A
}

impl <A> InboundPublishManager<A>
where A: MqttAck
{
    pub async fn dipatch(publish: Publish) {
        unimplemented!()
    }

    pub fn register_filter(topic: String) -> UnboundedReceiver<(Publish, AckToken)> {
        unimplemented!()
    }
}