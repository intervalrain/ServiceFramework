#!/bin/bash

IMAGE_NAME="edge-coa/edgesync_service_framework"

docker rmi ${IMAGE_NAME}:latest
docker rmi ${IMAGE_NAME}:latest

docker -D build -f Dockerfile -t ${IMAGE_NAME}:latest . 
if [ $? -eq 0 ]; then
    echo "Build ${IMAGE_NAME} completed"
else
    echo "Build ${IMAGE_NAME} failed"
    exit 1
fi

#docker tag edge-coa/digitaltwin_shadow_agent:latest edge-coa/digitaltwin_shadow_agent:1.0.0
#docker tag edge-coa/digitaltwin_shadow_agent_dbmigrator:latest edge-coa/digitaltwin_shadow_agent_dbmigrator:1.0.0
docker images | grep edge-coa
