#!/bin/bash

PROJ_NAME="edgesync_service_framework"
BUILD_DATE=$(date +%Y%m%d)
IMAGE_NAME="edge-coa/${PROJ_NAME}:latest"
OUTPUT_FOLDER="./release"

docker rmi ${IMAGE_NAME}
docker rmi ${IMAGE_NAME}

docker -D build -f Dockerfile -t ${IMAGE_NAME} . 
if [ $? -eq 0 ]; then
    echo "Build ${IMAGE_NAME} completed"
else
    echo "Build ${IMAGE_NAME} failed"
    exit 1
fi

docker images | grep ${PROJ_NAME}

mkdir -p ${OUTPUT_FOLDER}
CONTAINER_ID=$(docker run -d ${IMAGE_NAME} sleep infinity)
docker exec -it ${CONTAINER_ID} tar zcvf /app/${PROJ_NAME}.${BUILD_DATE}.dll.tgz ./out
docker cp $CONTAINER_ID:/app/${PROJ_NAME}.${BUILD_DATE}.dll.tgz ${OUTPUT_FOLDER}/

docker exec -it ${CONTAINER_ID} tar zcvf /app/${PROJ_NAME}.${BUILD_DATE}.pkg.tgz ./pkg
docker cp $CONTAINER_ID:/app/${PROJ_NAME}.${BUILD_DATE}.pkg.tgz ${OUTPUT_FOLDER}/

docker stop $CONTAINER_ID && docker rm $CONTAINER_ID
