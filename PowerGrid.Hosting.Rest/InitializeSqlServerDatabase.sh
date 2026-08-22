#!/bin/bash

#
# Copyright 2026 Alastair Wyse https://github.com/alastairwyse/PowerGrid/)
# 
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# 
#     http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

# ------------------------------------------------------------------------------
#
# Script: InitializeSqlServerDatabase.sh
# Description: Creates the default Powergrid database and objects in a SQL 
#   Server instance.
#
# Platform: Ubuntu Linux 20.04 (focal)
#
# Arguments:
#   $1 - The hostname or ip address of the SQL Server instance 
#   $2 - The SQL Server port 
#   $3 - The SQL Server user name 
#   $4 - The SQL Server password
#
# Usage Example:
#   ./InitializeSqlServerDatabase.sh 127.0.0.1 1433 sa mypassword
#
# ------------------------------------------------------------------------------

if [ -z "$1" ] || [ -z "$2" ] || [ -z "$3" ] || [ -z "$4" ]
then
    echo "ERROR: Required parameter not specified."
    echo "USAGE: InitializeSqlServerDatabase.sh [host/ip] [port] [username] [password]"
    exit 1
fi

# Install dependecies
echo "installing dependecies..."
curl https://packages.microsoft.com/keys/microsoft.asc | tee /etc/apt/trusted.gpg.d/microsoft.asc
curl https://packages.microsoft.com/config/ubuntu/20.04/prod.list | tee /etc/apt/sources.list.d/mssql-release.list
apt-get update
export ACCEPT_EULA=y

# Install mssql tools
echo "Installing mssql tools..."
apt-get install -y mssql-tools18 unixodbc-dev

# Retrieve and run the script to create the database objects
echo "Creating database schema..."
curl -O https://raw.githubusercontent.com/alastairwyse/PowerGrid/refs/heads/main/PowerGrid.Persistence.SqlServer/Resources/CreateDatabase.sql

/opt/mssql-tools18/bin/sqlcmd -S $1,$2 -U $3 -P $4 -C -i CreateDatabase.sql