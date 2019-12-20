Option Explicit

if WScript.Arguments.Count < 2 Then
    WScript.Echo "SYNTAX: source.csv output.xlsx"
    Wscript.Quit
End If

Const xlExcel12 = 50
Const xlOpenXMLWorkbook = 51
Const xlOpenXMLStrictWorkbook = 61

Dim srcFile, dstFile
Dim oExcel, oBook

srcFile = Wscript.Arguments.Item(0)
dstFile = Wscript.Arguments.Item(1)

Set oExcel = CreateObject("Excel.Application")
Set oBook = oExcel.Workbooks.Open(srcFile)
oBook.SaveAs dstFile, xlOpenXMLWorkbook
oBook.Close False
oExcel.Quit
WScript.Echo "Done!"
