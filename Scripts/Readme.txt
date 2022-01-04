Here are samples of scripts
all of them can be loaded using following command

dotnet OMSamples.dll callflow fromfolder name=<path_to_this_folder>

(see ReloadCallFlowSamples.cmd provided in ../bin/Debug/netcoreap2.1/)

Samples are deployed with TEST_DEPLOYMENT property and creates CallFlow script route point with name
<SubFolder>.<FileNameWithoutExtension>
To create CallFlow script route point without . - simply create subfolder and put obly one .cs (without name, only extension)
Supported following name:
any alphanumeric combinations
* and # also can be used, but they should make conflicts with dial codes configured on PBX.


