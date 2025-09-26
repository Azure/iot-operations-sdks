# codegen2

This folder contains the beginnings of a fork of the `codegen` folder.

The plan of record is to switch our modeling language from DTDL to WoT.
The largest part of this effort will be changing the ProtocolCompiler to ingest WoT Thing Descriptions instead of DTDL models.

Rather than attempting to evolve the extant ProtocolCompiler to ingest WoT while preserving its DTDL support for legacy usage, we are starting a new solution for a new ProtocolCompiler.
As the architecture of the new codebase develops, portions of the old ProtocolCompiler will be copied into the new one.

This folder will contain the new ProtocolCompiler while it is under development.
