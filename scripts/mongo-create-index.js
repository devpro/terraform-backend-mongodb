db.tf_state.createIndex({ "tenant": 1, "name": 1 });
//TODO: use tf_state_revisions
db.tf_state_lock.createIndex({ "tenant": 1, "name": 1 });
