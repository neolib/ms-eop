# autobcc helper tool

This program accepts an input csproj filepath and outputs a list of dependent
projects and commands ready to be pasted and run in CoreXT environment.

## Commandline Arguments:

autobcc [project.csproj] [/out output.txt]

If no input file specified, it will use the one found in current directory.

## Special Notes:

The program appends to output.txt if specified; and it does not append duplicate
dependent project files by examing the content of output.txt.

-=|E.O.F|=-
