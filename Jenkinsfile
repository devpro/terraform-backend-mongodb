pipeline {
  agent {
    kubernetes {
      yaml '''
        apiVersion: v1
        kind: Pod
        spec:
          containers:
          - name: buildkit
            image: moby/buildkit:rootless
            securityContext:
              runAsUser: 1000
              runAsGroup: 1000
            env:
            - name: BUILDKITD_FLAGS
              value: "--oci-worker-no-process-sandbox"
      '''
    }
  }

  environment {
    IMAGE_NAME = "tfbackend-mongodb"
    IMAGE_TAG  = "${env.GIT_COMMIT?.take(7) ?: 'dev'}"
    TARBALL    = "image.tar"
  }

  stages {

    stage('Checkout') {
      steps {
        git url: 'https://github.com/devpro/terraform-backend-mongodb.git',
            branch: env.BRANCH_NAME ?: 'main'
      }
    }

    stage('Build Image') {
      steps {
        container('buildkit') {
          sh """
            buildctl build \
              --frontend dockerfile.v0 \
              --local context=. \
              --local dockerfile=src/WebApi/Dockerfile \
              --output type=oci,dest=${TARBALL}
          """
        }
      }
    }

    stage('Download Wiz CLI') {
      steps {
        sh 'curl -o wizcli https://downloads.wiz.io/v1/wizcli/latest/wizcli-linux-amd64'
        sh 'chmod +x wizcli'
      }
    }

    stage('Wiz Image Scan') {
      steps {
        withCredentials([usernamePassword(
          credentialsId: 'wiz-credentials',
          usernameVariable: 'WIZ_CLIENT_ID',
          passwordVariable: 'WIZ_CLIENT_SECRET'
        )]) {
          sh "./wizcli scan container-image ${TARBALL} --json-output-file wiz-image-scan.json"
        }
      }
      post {
        always {
          archiveArtifacts artifacts: 'wiz-image-scan.json', allowEmptyArchive: true
        }
      }
    }

    // Post PR comment — commented out until output schema is confirmed from a real scan
    // stage('Post PR Comment') {
    //   when { changeRequest() }
    //   steps { ... }
    // }

    stage('Wiz Dir Scan') {
      when { branch 'main' }
      steps {
        withCredentials([usernamePassword(
          credentialsId: 'wiz-credentials',
          usernameVariable: 'WIZ_CLIENT_ID',
          passwordVariable: 'WIZ_CLIENT_SECRET'
        )]) {
          sh "./wizcli scan dir . --json-output-file wiz-dir-scan.json"
        }
      }
      post {
        always {
          archiveArtifacts artifacts: 'wiz-dir-scan.json', allowEmptyArchive: true
        }
      }
    }

  }
}
