#!/bin/sh
set -e

# Substitute only the API_BACKEND_URL variable into the nginx config template.
# Nginx variables ($uri, $proxy_host, etc.) are left untouched.
envsubst '${API_BACKEND_URL}' \
    < /etc/nginx/nginx.conf.template \
    > /etc/nginx/conf.d/default.conf

exec nginx -g 'daemon off;'
