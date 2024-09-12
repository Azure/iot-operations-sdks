from os.path import join

cases_per_test = 4

ser_formats = ('avro', 'cbor', 'json', 'proto2', 'proto3')

topic_types =  ('Together',                                     'Separate')
cmd_topics =   ('vehicles/{modelId}/command/{commandName}',   'vehicles/{modelId}/{executorId}/command/{commandName}')
telem_topics = ('vehicles/{modelId}/{senderId}/telemetry',    'vehicles/{modelId}/{senderId}/telemetry/{telemetryName}')

gen_root = join('..', 'generated')

in_root = join('..', 'input')
template_dir = join(in_root, 'templates')
pattern_dir = join(in_root, 'patterns')
support_dir = join(in_root, 'support')
