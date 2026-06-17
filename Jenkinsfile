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
            args:
            - --oci-worker-no-process-sandbox
            resources:
              requests:
                ephemeral-storage: "4Gi"
            securityContext:
              runAsUser: 1000
              runAsGroup: 1000
              seccompProfile:
                type: Unconfined
              appArmorProfile:
                type: Unconfined
            volumeMounts:
            - mountPath: /home/user/.local/share/buildkit
              name: buildkit-cache
          - name: jnlp
            resources:
              requests:
                ephemeral-storage: "2Gi"
          volumes:
          - name: buildkit-cache
            emptyDir: {}
      '''
    }
  }

  environment {
    IMAGE_NAME = "tfbackend-mongodb"
    IMAGE_TAG  = "${env.GIT_COMMIT?.take(7) ?: 'dev'}"
    TARBALL    = "tfbackend-mongodb-${env.GIT_COMMIT?.take(7) ?: 'dev'}.tar"
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
              --local dockerfile=src/WebApi \
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

    stage('Post PR Comment') {
      when { changeRequest() }
      steps {
        script {
          def d = readJSON file: 'wiz-image-scan.json'
          def verdict = d.status.verdict
          def critical = d.result.analytics.vulnerabilities.criticalCount
          def high = d.result.analytics.vulnerabilities.highCount
          def medium = d.result.analytics.vulnerabilities.mediumCount
          def secrets = d.result.analytics.secrets.totalCount
          def report = d.reportUrl

          def prCommentBody = """## Wiz Image Scan — ${verdict}

| Category | Count |
|---|---|
| 🔴 Critical | ${critical} |
| 🟠 High | ${high} |
| 🟡 Medium | ${medium} |
| 🔑 Secrets | ${secrets} |

[View in Wiz](${report})

_[Jenkins build](${env.BUILD_URL})_"""

          withCredentials([usernamePassword(
            credentialsId: 'github-devpro-org-scan', 
            usernameVariable: 'GH_USER', 
            passwordVariable: 'GH_TOKEN'
          )]) {
            writeJSON file: 'comment.json', json: [body: prCommentBody]
            
            def prNumber = env.CHANGE_ID
            
            sh """
              curl -s -u "\${GH_USER}:\${GH_TOKEN}" \
                   -H "Content-Type: application/json" \
                   -X POST \
                   -d @comment.json \
                   "https://api.github.com/repos/devpro/terraform-backend-mongodb/issues/${prNumber}/comments"
            """
          }
        }
      }
    }

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
