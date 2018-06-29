#!/usr/bin/env bash
##########################################################################
# This is the Cake bootstrapper script for Linux and OS X.
# This file was downloaded from https://github.com/cake-build/resources
# Feel free to change this file to fit your needs.
##########################################################################

# Define directories.
SCRIPT_DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )


CAKE_VERSION=0.28.0
DOTNET_VERSION=2.1.101
DOTNET_INSTALLER_URI=https://dot.net/v1/dotnet-install.sh

TOOLS_DIR=$SCRIPT_DIR/tools
TOOLS_PROJ=$TOOLS_DIR/tools.csproj
TOOLS_PROJ_URI=https://raw.githubusercontent.com/SoftwarePioniere/SoftwarePioniere.Cake/master/tools.proj

NUGET_CONFIG=$TOOLS_DIR/nuget.config
NUGET_CONFIG_URI=https://raw.githubusercontent.com/SoftwarePioniere/SoftwarePioniere.Cake/master/nuget.config

PACKAGE_DIR=$TOOLS_DIR/cake.coreclr/$CAKE_VERSION
CAKE_DLL=$TOOLS_DIR/cake.coreclr/$CAKE_VERSION/Cake.dll

# Temporarily skip verification of addins.
export CAKE_SETTINGS_SKIPVERIFICATION='true'

# Define default arguments.
TARGET="Default"
CONFIGURATION="Release"
VERBOSITY="verbose"
DRYRUN=
SCRIPT_ARGUMENTS=()

# Parse arguments.
for i in "$@"; do
    case $1 in
        -t|--target) TARGET="$2"; shift ;;
        -c|--configuration) CONFIGURATION="$2"; shift ;;
        -v|--verbosity) VERBOSITY="$2"; shift ;;
        -d|--dryrun) DRYRUN="-dryrun" ;;
        --) shift; SCRIPT_ARGUMENTS+=("$@"); break ;;
        *) SCRIPT_ARGUMENTS+=("$1") ;;
    esac
    shift
done

# Make sure the tools folder exist.
if [ ! -d "$TOOLS_DIR" ]; then
  mkdir "$TOOLS_DIR"
fi


###########################################################################
# INSTALL .NET CORE CLI
###########################################################################
echo "Check for Installing .NET Core CLI $DOTNET_VERSION"

DOTNETCLIVERSIONGLOBALINSTALLED=0

VERSIONS=$(dotnet --version)
# echo "Versions 1"
# echo $VERSIONS

echo "Found .NET CLI Versions:"
for s in "$VERSIONS"; do

    echo $s

    if [ $DOTNETCLIVERSIONGLOBALINSTALLED = 0  ]; then
        #echo "0"

        if [[ $s = *$DOTNET_VERSION* ]]; then
            DOTNETCLIVERSIONGLOBALINSTALLED=1
            # echo "It's there!"
        fi
    fi
done



if [ $DOTNETCLIVERSIONGLOBALINSTALLED = 0  ]; then
    echo "Preparing local .NET Core CLI"

    if [ ! -d "$TOOLS_DIR/.dotnet" ]; then
        mkdir "$TOOLS_DIR/.dotnet"
    fi


    if [ ! -d "$TOOLS_DIR/.dotnet/dotnet-install.sh" ]; then
        echo "Installing .NET Core CLI SDK local.."
        curl -Lsfo "$TOOLS_DIR/.dotnet/dotnet-install.sh" $DOTNET_INSTALLER_URI

        bash "$TOOLS_DIR/.dotnet/dotnet-install.sh" --version $DOTNET_VERSION --install-dir $TOOLS_DIR/.dotnet --no-path
    else
        echo ".NET Core CLI already local Installed"
    fi

    export PATH="$TOOLS_DIR/.dotnet":$PATH
else
    echo ".NET CLI Version global Installed"
fi

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1

###########################################################################
# INSTALL CAKE
###########################################################################

if [ ! -f "$TOOLS_PROJ" ]; then
    echo "Installing tools.proj"
    curl -Lsfo $TOOLS_PROJ $TOOLS_PROJ_URI
fi

if [ ! -f "$NUGET_CONFIG" ]; then
    echo "Installing nuget.config"
    curl -Lsfo $NUGET_CONFIG $NUGET_CONFIG_URI
fi

if [ ! -f "$CAKE_DLL" ]; then
    echo "Installing Cake.Core"
    dotnet add $TOOLS_PROJ package Cake.CoreCLR -v $CAKE_VERSION --package-directory $PACKAGE_DIR
fi

# Make sure that Cake has been installed.
if [ ! -f "$CAKE_DLL" ]; then
    echo "Could not find Cake.dll at '$CAKE_DLL'."
    exit 1
fi

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

# Start Cake
exec dotnet "$CAKE_DLL" build.cake --verbosity=$VERBOSITY --configuration=$CONFIGURATION --target=$TARGET $DRYRUN "${SCRIPT_ARGUMENTS[@]}"
