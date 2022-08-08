#!/bin/bash

echo "Running release script with [SOURCE_PATH=${SOURCE_PATH}, TARGET_PATH=${TARGET_PATH}, args=$@]"

VER=$(echo $1 | sed 's/-[a-z]*//g')
sed -i -e '/AssemblyVersion/s/\".*\"/\"'$VER'\"/' \
    ${SOURCE_PATH}/AssemblyInfo.cs

unity-packer pack Mirage.ClientSidePrediction.unitypackage \
    ${SOURCE_PATH} ${TARGET_PATH} \
    README.md ${TARGET_PATH}/README.md \
    CHANGELOG.md ${TARGET_PATH}/CHANGELOG.md
