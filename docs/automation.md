# Automation

## Platform

* [GitLab](https://gitlab.com/) is used to run the CI (Continuous Integration) & PKG (Packaging = Continuous Delivery) pipeline,
which is defined in [`.gitlab-ci.yml`](../.gitlab-ci.yml) file.

## Setup

### GitLab > Settings > CI/CD > Variables

* Add the following variables

Name | Value | Protected | Masked
---- | ----- | --------- | ------
SONAR_ORGANIZATION | Sonar Organization | No | No
SONAR_PROJECTKEY | Sonar Project Key | No | No
SONAR_HOSTURL | Sonar Instance URL | No | No
SONAR_TOKEN | Sonar Key | No | Yes
