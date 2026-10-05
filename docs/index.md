# Welcome

A simple, standards-compliant HTTP backend for [Terraform](https://www.terraform.io) (and [OpenTofu](https://opentofu.org/)) that stores state files in MongoDB.

!!! tip

    Instead of relying on vendor-specific storage or local files, this backend uses MongoDB, a mature, horizontally scalable document database, as the storage layer for Terraform state.
    
    Since Terraform state is already JSON, MongoDB is a natural and efficient fit.

## Key features

- **Full Terraform HTTP backend compliance**: works out-of-the-box with terraform `{ backend "http" }` (and OpenTofu)
- **Leverages MongoDB strengths**: high availability, replication, sharding, and strong performance for JSON documents
- **No vendor lock-in**: the MongoDB cluster stays under the organization's control (self-hosted, Atlas, etc.)
- **Fine-grained access control**: per-tenant isolation
- **State file encryption at rest**: optional, using MongoDB's encrypted storage engine
- **Locking support implemented**: Terraform checks and prevents concurrent modifications
- **Minimal dependencies**: simple open-source code, shipped in an image using SUSE BCI for security and performance
- **State change history**: every state update is recorded as a JSON diff with workspace and timestamp
- **Queryable state**: the state is stored as a BSON document, so other applications can query it by resource attribute

## When to use this backend

- MongoDB already runs in the organization
- A highly available, globally distributed state store is wanted without adding another vendor
- Strong RBAC and encryption controls are required
- Running one container is preferred over managing S3
- Terraform is to be integrated with an infrastructure management system
- A single, highly available source of truth is wanted
