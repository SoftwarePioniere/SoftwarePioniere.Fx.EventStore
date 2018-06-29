
#######################
# Delete Junk
ls -Recurse -include 'bin','obj', 'TestResults' -Path .\   |
  foreach {
    remove-item $_.FullName -recurse -force
    write-host deleted $_.FullName
}

#Cleanup Nuge Cache
#ls -Recurse -Path $env:HOMEDRIVE$env:HOMEPATH\appdata\local\nuget\cache |
#  foreach {
#    remove-item $_.FullName -recurse -force
#    write-host deleted $_.FullName
#}

