# Welcome

This project provides an HTTP backend for Terraform and OpenTofu that will save and manage state data from a MongoDB database.

As the state is a JSON content, it makes sense to use the best-in-class database technology to work with it.

The goal is to:

- provide an highly available storage
- secure sensitive data
- query the data without an infrastructure automation tool
