use azure_iot_operations_mqtt::interface::ManagedClient;

use crate::libsgen::common_types::common_options::CommandOptionsBuilder;
use crate::libsgen::dtmi_ms_adr_SchemaRegistry__1::client;

pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    get_command_invoker: client::GetCommandInvoker<C>,
    put_command_invoker: client::PutCommandInvoker<C>,
}
