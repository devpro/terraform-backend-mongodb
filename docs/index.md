# Welcome

This project provides an HTTP backend for [Terraform](https://www.terraform.io) and [OpenTofu](https://opentofu.org/) that will save and manage state data in a [MongoDB](https://www.mongodb.com/) database.

As the state is a JSON content, it makes sense to use the best-in-class database technology to work with it.

The goal is to:

- provide an highly available, and performant, storage
- share a secured access to sensitive information
- value the infrastructure data
