write-host "Updating database" 
write-host ""
dotnet ef database update -v --project src\API
write-host ""
write-host "Done." 