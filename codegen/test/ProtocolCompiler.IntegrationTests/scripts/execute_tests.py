import time
from datetime import timedelta
from glob import glob
from os.path import basename, join, splitext
from subprocess import run

from test_defs import ser_formats, topic_types, cmd_topics, telem_topics, gen_root, template_dir

def exe_service_test(format, service_name):
    test_name = service_name + 'As' + format.capitalize()
    test_root = join(gen_root, test_name)
    standalone_path = join(test_root, 'dotnet', 'standalone')
    run(['dotnet', 'test', standalone_path])

start = time.time()
test_count = 0
for test_template in glob(join(template_dir, '*.tt')):
    for format in ser_formats:
        for variant in range(len(topic_types)):
            service_name = splitext(basename(test_template))[0] + topic_types[variant]
            exe_service_test(format, service_name)
            test_count = test_count + 1
end = time.time()
print(f"Executed {test_count} tests in {timedelta(seconds=end - start)}")
