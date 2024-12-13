using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Connector
{
    internal class DatasetSamplingContext
    {
        public IDatasetSampler DatasetSampler { get; set; }

        public Timer DatasetSamplingTimer { get; set; }

        public DatasetSamplingContext(IDatasetSampler datasetSampler, Timer datasetSamplingTimer)
        {
            DatasetSampler = datasetSampler;
            DatasetSamplingTimer = datasetSamplingTimer;
        }
    }
}
