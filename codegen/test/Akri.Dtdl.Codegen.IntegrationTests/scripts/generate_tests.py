import time
from datetime import timedelta
from glob import glob
from os import makedirs
from os.path import basename, dirname, exists, join, splitext
from shutil import rmtree, copy
from subprocess import run

from test_defs import cases_per_test, ser_formats, topic_types, cmd_topics, telem_topics, gen_root, template_dir, pattern_dir, support_dir

ser_format_folders = {
    'avro' : 'AVRO',
    'cbor': 'CBOR',
    'json': 'JSON',
    'proto2': 'protobuf',
    'proto3': 'protobuf'
}

def expand_model(model_path, test_template, model_id, format, variant):
    makedirs(dirname(model_path))
    arg_model_id = '!!modelId!' + model_id
    arg_format = '!!format!' + format
    arg_cmd_topic = '!!cmdTopic!' + cmd_topics[variant]
    arg_telem_topic = '!!telemTopic!' + telem_topics[variant]
    run([ 'TextTransform', test_template, '-a', arg_model_id, '-a', arg_format, '-a', arg_cmd_topic, '-a', arg_telem_topic, '-out', model_path ])

def gen_schema(model_path, schemas_dir):
    makedirs(schemas_dir)
    schema_gen_exe = join('..', '..', '..', 'src', 'Akri.Dtdl.Codegen', 'Akri.Dtdl.Codegen.SchemaGenerator', 'bin', 'Debug', 'net7.0', 'Akri.Dtdl.Codegen.SchemaGenerator.exe')
    run([schema_gen_exe, model_path, schemas_dir])

def gen_from_avro(lib_path, schemas_dir, namespace):
    avro_path = join(schemas_dir, namespace)
    makedirs(lib_path)
    for avro_file in glob(join(avro_path, '*.avsc')):
        run(['avrogen', '-s', avro_file, lib_path])

def gen_from_json(lib_path, schemas_dir, namespace):
    lib_ns_path = join(lib_path, namespace)
    type_gen_exe = join('..', '..', '..', 'src', 'Akri.Dtdl.Codegen', 'Akri.Dtdl.Codegen.TypeGenerator', 'bin', 'Debug', 'net7.0', 'Akri.Dtdl.Codegen.TypeGenerator.exe')
    makedirs(lib_ns_path)
    for json_schema in glob(join(schemas_dir, namespace, '*.schema.json')):
        run([type_gen_exe, 'csharp', json_schema, namespace, lib_ns_path])

def gen_from_proto(lib_path, schemas_dir, namespace):
    lib_ns_path = join(lib_path, namespace)
    proto_path = join(schemas_dir, namespace)
    inc_path = join('..', '..', '..', 'include')
    makedirs(lib_ns_path)
    for proto_file in glob(join(proto_path, '*.proto')):
        run(['protoc', '--csharp_out=' + lib_ns_path, '--proto_path=' + proto_path, '--proto_path=' + inc_path, basename(proto_file)])
    run(['protoc', '--csharp_out=' + lib_ns_path, '--proto_path=' + proto_path, '--proto_path=' + inc_path, 'dtdl/protobuf/dtdl_options.proto'])

def gen_envoy(lib_path, namespace, annex_path):
    envoy_gen_exe = join('..', '..', '..', 'src', 'Akri.Dtdl.Codegen', 'Akri.Dtdl.Codegen.EnvoyGenerator', 'bin', 'Debug', 'net7.0', 'Akri.Dtdl.Codegen.EnvoyGenerator.exe')
    run([envoy_gen_exe, 'csharp', annex_path, join(lib_path, namespace)])

def copy_serializers(lib_path, namespace, format):
    lib_ns_path = join(lib_path, namespace)
    for file in glob(join('..', '..', '..', '..', '..', 'lib', 'dotnet', 'test', 'Azure.Iot.Operations.Protocol.UnitTests', 'Serializers', ser_format_folders[format], '*.cs')):
        copy(file, lib_ns_path)

def gen_cases(annex_path, model_path, case_dir):
    case_gen_exe = join('..', 'Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator', 'bin', 'Debug', 'net7.0', 'Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator.exe')
    pattern_path = join(pattern_dir, 'TestPatternCatalogue.json')
    makedirs(case_dir)
    run([case_gen_exe, annex_path, model_path, pattern_path, case_dir, str(cases_per_test)])

def gen_test_code(annex_path, model_path, case_dir, test_root):
    code_gen_exe = join('..', 'Akri.Dtdl.Codegen.IntegrationTests.TestCodeGenerator', 'bin', 'Debug', 'net7.0', 'Akri.Dtdl.Codegen.IntegrationTests.TestCodeGenerator.exe')
    run([code_gen_exe, 'csharp', annex_path, model_path, case_dir, test_root])

def gen_service_test(test_template, format, variant, service_name):
    model_id = 'dtmi:'+ format + 'Test:' + service_name + ';1'
    test_name = service_name + 'As' + format.capitalize()
    namespace = 'dtmi_'+ format + 'Test_' + service_name + '__1'
    test_root = join(gen_root, test_name)
    if exists(test_root):
        rmtree(test_root)
    makedirs(test_root)
    model_path = join(test_root, 'model', test_name + '.json')
    expand_model(model_path, test_template, model_id, format, variant)
    schemas_dir = join(test_root, 'schemas')
    gen_schema(model_path, schemas_dir)
    lib_path = join(test_root, 'dotnet', 'library')
    match format:
        case 'avro':
            gen_from_avro(lib_path, schemas_dir, namespace)
        case 'json':
            gen_from_json(lib_path, schemas_dir, namespace)
        case 'cbor':
            gen_from_json(lib_path, schemas_dir, namespace)
        case 'proto2':
            gen_from_proto(lib_path, schemas_dir, namespace)
        case 'proto3':
            gen_from_proto(lib_path, schemas_dir, namespace)
    annex_path = join(schemas_dir, namespace, service_name + '.annex.json')
    gen_envoy(lib_path, namespace, annex_path)
    copy_serializers(lib_path, namespace, format)
    case_dir = join(test_root, 'cases')
    gen_cases(annex_path, model_path, case_dir)
    gen_test_code(annex_path, model_path, case_dir, test_root)

start = time.time()
test_count = 0
for test_template in glob(join(template_dir, '*.tt')):
    for format in ser_formats:
        for variant in range(len(topic_types)):
            service_name = splitext(basename(test_template))[0] + topic_types[variant]
            gen_service_test(test_template, format, variant, service_name)
            test_count = test_count + 1
run([ 'TextTransform', join(support_dir, 'DotNetTestSln.tt'), '-out', join(gen_root, 'Akri.Dtdl.Codegen.IntegrationTests.sln') ])
end = time.time()
print(f"Generated {test_count} tests in {timedelta(seconds=end - start)}")
