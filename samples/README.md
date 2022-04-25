# Samples

## Demonstration

* Run the commands

```bash
cd samples

cat main.tf

terraform init \
  -backend-config="address=http://localhost:5293/state/demo_kalosyni" \
  -backend-config="lock_address=http://localhost:5293/state/demo_kalosyni/lock" \
  -backend-config="unlock_address=http://localhost:5293/state/demo_kalosyni/lock" \
  -backend-config="username=demo_username" \
  -backend-config="password=demo_token" \
  -backend-config="lock_method=POST" \
  -backend-config="unlock_method=DELETE" \
  -backend-config="retry_wait_min=5"
```
