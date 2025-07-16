#!/bin/bash

if [ "$#" -ne 3 ]; then
    echo "Usage: $0 <username> <password> <tenant>"
    exit 1
fi

USERNAME="$1"
PASSWORD="$2"
TENANT="$3"

if ! command -v htpasswd &> /dev/null; then
    echo "Error: htpasswd is not installed."
    echo "Please install apache2-utils (on Debian/Ubuntu) or httpd-tools (on CentOS/RHEL)."
    echo "Installation commands:"
    echo "  Debian/Ubuntu: sudo apt-get install apache2-utils"
    echo "  CentOS/RHEL: sudo yum install httpd-tools"
    exit 1
fi

SCRIPT_DIR=$(dirname "$(realpath "$0")")
JS_FILE="$SCRIPT_DIR/add-user.js"

HASH=$(htpasswd -bnBC 10 "" "$PASSWORD" | tr -d ':\n')
if [ $? -ne 0 ] || [ -z "$HASH" ]; then
    echo "Error: Failed to generate BCrypt hash."
    exit 1
fi

cat > "$JS_FILE" << EOF
db.user.insertOne({
    username: '$USERNAME',
    password_hash: '$HASH',
    tenant: '$TENANT'
});
db.user.createIndex({ "username": 1 }, { unique: true });
EOF

echo "Generated "$JS_FILE" successfully"
