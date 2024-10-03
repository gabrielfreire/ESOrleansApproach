$projectName = "GrainInterfaces"
$serviceName = "ESOrleansApproach"
write-output ""

$libraryExists = Test-Path ./src/$projectName

if ($libraryExists) 
{
	write-host "Publishing $projectName..." -fore blue
	write-host ""
	write-host "Command: ./build --target push --configuration Release --service-name $serviceName --project-name $projectName" -fore green
	write-host ""
	write-host ""
	./build --target push --configuration Release --service-name $serviceName --project-name $projectName
}
else
{
	write-host ""
	write-host "Library: $projectName does not exist!" -fore red
	write-host ""
	write-host ""
}
