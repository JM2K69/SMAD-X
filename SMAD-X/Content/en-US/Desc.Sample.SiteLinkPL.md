# 🔗 DEFAULTIPSITELINK — Link Paris ↔ Lyon

AD replication link between sites **Site-Paris** and **Site-Lyon**.

## Parameters
| Parameter | Value |
|-----------|-------|
| Transport | IP (RPC/IP) |
| Cost | 100 |
| Interval | 15 minutes |
| Schedule | Always available |

## ⚠️ Best practices
- Adjust replication interval based on available WAN bandwidth
- For WAN links < 512 Kbps, consider SMTP as an alternative transport
- Use `repadmin /showrepl` to diagnose replication delays