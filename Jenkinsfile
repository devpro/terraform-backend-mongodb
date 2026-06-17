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
    MAIN_BRANCH = 'main'
    IMAGE_SCAN_REPORT = 'wiz-image-scan.json'
    DIR_SCAN_REPORT = 'wiz-dir-scan.json'
  }

  stages {
    stage('Checkout') {
      steps {
        git url: 'https://github.com/devpro/terraform-backend-mongodb.git',
            branch: env.BRANCH_NAME ?: MAIN_BRANCH

        script {
          def props = readFile('Directory.Build.props')
          def matcher = (props =~ /<VersionPrefix>(\d+\.\d+)\.\d+<\/VersionPrefix>/)
          if (matcher.find()) {
            env.VERSION = matcher.group(1)
          } else {
            env.VERSION = 'ci'
          }
          matcher = null

          def commitHash = env.GIT_COMMIT ? env.GIT_COMMIT.take(7) : env.BRANCH_NAME
          env.TARBALL = "tfbackend-mongodb-${env.VERSION}.${commitHash}.tar"
        }
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
        withWizCredentials {
          sh "./wizcli scan container-image ${TARBALL} --json-output-file ${IMAGE_SCAN_REPORT}"
        }
      }
      post {
        always {
          archiveArtifacts artifacts: IMAGE_SCAN_REPORT, allowEmptyArchive: true
        }
      }
    }

    stage('Post PR Comment') {
      when { changeRequest() }
      steps {
        script {
          def d = readJSON file: IMAGE_SCAN_REPORT
          def verdict = d.status?.verdict ?: 'UNKNOWN'
          def report = d.reportUrl ?: 'https://app.wiz.io'

          def critical = 0
          def high = 0
          def medium = 0

          def sbomArtifacts = d.result?.vulnerableSBOMArtifactsByNameVersion

          if (sbomArtifacts) {
            sbomArtifacts.each { artifact ->
              def severities = artifact?.vulnerabilityFindings?.severities
              if (severities) {
                critical += (severities.criticalCount ?: 0)
                high += (severities.highCount ?: 0)
                medium += (severities.mediumCount ?: 0)
              }
            }
          }

          def prCommentBody = """## Wiz Image Scan — ${verdict}

| Category | Count |
|---|---|
| 🔴 Critical | ${critical} |
| 🟠 High | ${high} |
| 🟡 Medium | ${medium} |

[View Detail Report in Wiz](${report})

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
      when {
        environment name: 'BRANCH_NAME', value: MAIN_BRANCH
      }
      steps {
        withWizCredentials {
          sh "./wizcli scan dir . --json-output-file ${DIR_SCAN_REPORT}"
        }
      }
      post {
        always {
          archiveArtifacts artifacts: DIR_SCAN_REPORT, allowEmptyArchive: true
        }
      }
    }
  }
}

def withWizCredentials(Closure body) {
  withCredentials([usernamePassword(
    credentialsId: 'wiz-credentials',
    usernameVariable: 'WIZ_CLIENT_ID',
    passwordVariable: 'WIZ_CLIENT_SECRET'
  )]) {
    body()
  }
}
