# autobcc helper tool

This program accepts an input csproj filepath and outputs a list of dependent
projects and commands ready to be pasted and run in CoreXT environment.

## Commandline Arguments:

autobcc [project.csproj]

If no input file specified, it will use the one found in current directory.

## Special Notes:

You can use redirection operator to output to a batch file:

autobcc >build.cmd

and run build.cmd.

-=|E.O.F|=-
