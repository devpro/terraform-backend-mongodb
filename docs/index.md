# Welcome

A simple, standards-compliant HTTP backend for [Terraform](https://www.terraform.io) (and [OpenTofu](https://opentofu.org/)) that stores state files in MongoDB.

!!! tip

	Instead of relying on vendor-specific storage or local files, this backend lets you use MongoDB - a mature, horizontally scalable document database - as the storage layer for your Terraform state.
	
	Since Terraform state is already JSON, MongoDB is a natural and efficient fit.

## Key features

- **Full Terraform HTTP backend compliance** - works out-of-the-box with terraform `{ backend "http" }` (and OpenTofu)
- **Leverages MongoDB strengths** - high availability, replication, sharding, and strong performance for JSON documents
- **No vendor lock-in** - you control your MongoDB cluster (self-hosted, Atlas, Cosmos DB, etc.)
- **Fine-grained access control** - per-workspace (tenant) isolation
- **State file encryption at rest** - optional server-side encryption using MongoDB's native encrypted storage engine or client-side encryption
- **Locking support implemented** - Terraform checks and prevents concurrent modifications
- **Minimal dependencies** - simple open-source code, shipped in an image using SUSE BCI for security and performance
- **Audit trail** - all state operations logged with workspace, user/agent, and timestamp

## When to use this backend

- You already run MongoDB in your organization
- You want a highly available, globally distributed state store without adding another vendor
- You need strong RBAC and encryption controls
- You prefer running one container instead of managing S3
- You wish to integrate Terraform with your infrastructure management system
- You want to a single, highly available source of truth
