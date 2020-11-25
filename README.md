# PrenatalServiceDebugger
A Windows service debugging utility, that is designed to improve the debugging experience of a service start-up routine.
Inspired by https://github.com/lowleveldesign/diagnostics-tools/tree/master/WinSvcDiagnostics

# Build Status
Branch | Status
--- | ---
develop | [![Build Status (develop)](https://dev.azure.com/stefanortner/PrenatalServiceDebugger/_apis/build/status/ohyeahitsstef.PrenatalServiceDebugger?branchName=develop)](https://dev.azure.com/stefanortner/PrenatalServiceDebugger/_build/latest?definitionId=1?branchName=develop)
 master | [![Build Status (develop)](https://dev.azure.com/stefanortner/PrenatalServiceDebugger/_apis/build/status/ohyeahitsstef.PrenatalServiceDebugger?branchName=master)](https://dev.azure.com/stefanortner/PrenatalServiceDebugger/_build/latest?definitionId=1?branchName=master)

# Usage
TODO

# Topshelf services
Topshelf checks if the current process is running as child of services.exe to decide whether to run as service or as console app. When the PrenatalServiceDebugger has created the process, Topshelf will falsely assume it should run as console app. To workaround this issue rename the PrenatalServiceDebugger.exe to services.exe.
