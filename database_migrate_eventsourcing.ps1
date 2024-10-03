$_migrationName = $Args[0]

if ($_migrationName -eq $null) 
{
	write-host "Migration name not found" -fore red
}
else
{
	write-host "Migrating..." -fore blue
	write-host "Creating Migration $_migrationName" -fore blue
	dotnet ef migrations add $_migrationName -v --project src\EventSourcing\ --startup-project src\API --output-dir SnapshotDB\Migrations\
	write-host "Migration $_migrationName created!" -fore blue
	write-host ""
}

