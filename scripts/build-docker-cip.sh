#!/bin/bash

VERSION="CIP"

docker rmi advantech/eoc/edgesync_service_framework:${VERSION}
docker rmi advantech/eoc/edgesync_service_framework:${VERSION}

docker -D build -f  Dockerfile -t advantech/eoc/edgesync_service_framework:${VERSION} . 
if [ $? -eq 0 ]; then
    echo "Build edgesync_service_framework completed"
else
    echo "Build edgesync_service_framework failed"
    exit 1
fi
