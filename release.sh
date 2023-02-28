#! /bin/sh
if [ -z "$2" ] ; then
  ns="'""$1 Beta'"
else
  ns="'""$1 $2'"
  if [ -z "$3" ] ; then
     ts='Release by gh'
  else
    ts="'$3'"
  fi
fi

if [ ! -r Unexpected.cs ] ; then
  echo $0 needs to be run from the project root directory as release version note title
  exit
fi

if [ -z "$1" ] ; then
  echo $0 v0.0.1 'notes' 'title'
  echo gh release list
  gh release list
else
  cp obj/Debug/net48/Unexpected.dll .
  echo 7z u bin/Debug/Unexpected.zip Unexpected.dll
  7z u bin/Debug/Unexpected.zip Unexpected.dll
  echo gh release create $1 -n "$ns" -t "$ts"  bin/Debug/Unexpected.zip
  gh release create $1 -n "$ns" -t "$ts"  bin/Debug/Unexpected.zip
  rm Unexpected.dll
fi
