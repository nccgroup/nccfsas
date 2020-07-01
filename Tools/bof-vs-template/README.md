# bof

This is a template project for building Cobalt Strike BOFs in Visual Studio.

If you prefer not to use the command line `cl.exe` or `mingw32` and are used to working with an IDE, this project should help with that.

## Using it

Just clone this repo and you can start writing code in `bof.c`, using the APIs detailed in [this Cobalt Strike post.](https://www.cobaltstrike.com/help-beacon-object-files)

If you change the default `demo` function name, then you will also need to change the entrypoint in the `cna\hello.cna` file.

## Building

The project settings are setup to compile a `.lib` file without linking it. There is then a post-build job that copies the `.obj` files into the `cna\bin\` folder as `bof.x64.o` and `bof.x86.o` respectively.

Once you have built the `.o` files, you just need to modify the `hello.cna` to pack your arguments correctly and modify the entrypoint and alias and you are all set!

## Testing it out

If you just want to give BOFs a try, first build the project in release mode for both x86 and x64, then copy the `cna\` folder and load the `hello.cna` file in Cobalt Strike. In a beacon session type `hello`.

This is the default example from the Cobalt Strike blog.

You should see the following output printed if successful:

```
[+] received output:
Message is Hello World with 1234 arg
```