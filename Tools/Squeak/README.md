# Squeak

Generate a standalone .net executable with hard coded arguments to leverage SQL CLR integration.

1. Open the Squeak GUI
2. Populate the connection details and supply a raw shellcode file
3. Generate the executable and run it

Code for the CLR is taken from the clrcode.cs file which must reside in the working directory of the Squeak.exe binary. Modifications to the code, for example to change the spawned binary, can be carried out within the Squeak GUI or by directly editing the clrcode.cs file. The file uses the string [RAW] as a placeholder for the shellcode.
