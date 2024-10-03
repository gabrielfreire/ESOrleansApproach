def IMAGE_NAME = "<account_name>/ESOrleansApproach"
def IMAGE_NAME_ARM = "<account_name>/ESOrleansApproach-arm"

def IMAGE_VERSION = "${env.BUILD_ID}"
def IMAGE_NAME_WITH_VERSION = "${IMAGE_NAME}:${IMAGE_VERSION}"
def IMAGE_NAME_WITH_VERSION_ARM = "${IMAGE_NAME_ARM}:${IMAGE_VERSION}"

def NAMESPACE = "smartplatform"
def KUBE_DEPLOYMENT_FILE = "kubernetes/deployment.yml"
def CONTAINER_NAME = "ESOrleansApproach"
def DEPLOYMENT_NAME = "ESOrleansApproach-pod"

def REPOSITORY_URL = "https://github.com/smart-platform/ESOrleansApproach.git"

def KUBE_CREDENTIALS_ID_PI = "pi-clusterk8s.config"
def KUBE_CREDENTIALS_ID_AZ = "az-clusterk8s.config"

def DOCKER_CREDENTIALS_ID = "dockerhub"
def GIT_CREDENTIALS_ID = "github"

def DOCKER_REGISTRY = "https://registry.hub.docker.com"
def DOCKERFILE_NAME = "Dockerfile"
def DOCKERFILE_NAME_ARM = "Dockerfile_arm"

def BRANCH = "master"

node {
    stage("Checkout source-code from GIT - Branch: ${BRANCH}") {
        script {
            git branch: "${BRANCH}", credentialsId: "${GIT_CREDENTIALS_ID}", url: "${REPOSITORY_URL}"
        }
    }
    stage('Build docker image with new version') {
        script {
            dockerimage = docker.build("${IMAGE_NAME_WITH_VERSION}", "-f src/${DOCKERFILE_NAME} src")
            dockerimage_arm = docker.build("${IMAGE_NAME_WITH_VERSION_ARM}", "-f src/${DOCKERFILE_NAME_ARM} src")
        }
    }
    stage('Push docker images for latest and next version tag') {
        script {
            docker.withRegistry("${DOCKER_REGISTRY}", "${DOCKER_CREDENTIALS_ID}") {
                dockerimage.push('latest')
                dockerimage.push("${IMAGE_VERSION}")
                dockerimage_arm.push('latest')
                dockerimage_arm.push("${IMAGE_VERSION}")
            }
        }
    }
    stage("Deploy app to kubernetes cluster using config: ${KUBE_CREDENTIALS_ID_PI} and ${KUBE_CREDENTIALS_ID_AZ}") {
        withKubeConfig([credentialsId: "${KUBE_CREDENTIALS_ID_PI}"]) {
            sh 'curl -LO "https://storage.googleapis.com/kubernetes-release/release/$(curl -s https://storage.googleapis.com/kubernetes-release/release/stable.txt)/bin/linux/amd64/kubectl"'
            sh 'chmod u+x ./kubectl'
            sh "./kubectl apply -f ${KUBE_DEPLOYMENT_FILE}"
            sh "./kubectl -n ${NAMESPACE} set image deployment/${DEPLOYMENT_NAME} ${CONTAINER_NAME}=${IMAGE_NAME_WITH_VERSION_ARM}"
        }
        withKubeConfig([credentialsId: "${KUBE_CREDENTIALS_ID_AZ}"]) {
            sh 'curl -LO "https://storage.googleapis.com/kubernetes-release/release/$(curl -s https://storage.googleapis.com/kubernetes-release/release/stable.txt)/bin/linux/amd64/kubectl"'
            sh 'chmod u+x ./kubectl'
            sh "./kubectl apply -f ${KUBE_DEPLOYMENT_FILE}"
            sh "./kubectl -n ${NAMESPACE} set image deployment/${DEPLOYMENT_NAME} ${CONTAINER_NAME}=${IMAGE_NAME_WITH_VERSION}"
        }
    }
}
