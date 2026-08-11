# Development Container Apps Migration Runner Cost Packet

**Region:** West US 2
**Status:** planning estimate only; not a quote or deployment authorization

The finite migration workload is expected to run for minutes per month, so
Container Apps Consumption execution should normally remain inside applicable
free grants or cost materially less than USD $1/month. Actual grants, meters,
and prices must be checked in Azure Pricing Calculator immediately before
approval.

| Component | Development estimate | Notes |
| --- | ---: | --- |
| Container Apps Job execution | $0–$1/month | One 0.5-vCPU/1-GiB replica for bounded manual runs |
| Container Apps environment | $0 fixed consumption charge | Usage and observability meters still apply |
| ACR Basic | approximately $5/month | Entra/Managed Identity protected; no private endpoint |
| Log Analytics | $0–$5/month | Small bounded logs, 30-day retention; ingestion-dependent |
| Delegated subnet/private DNS link | $0–$1/month | Existing VNet and SQL private DNS are reused |
| Optional ACR Premium/private endpoint | excluded; roughly $50+/month | Use only after a separate threat/cost review |

Expected incremental development total: approximately **$5–$12/month**, plus
taxes and any actual telemetry/network egress. Contract pricing may differ.
Configure a low ingestion budget and cost alert before enabling the Job.

The starter's built-in Log Analytics Reader assignment is access control only
and adds no direct cost. It is scoped to the dedicated workspace. The table's
Log Analytics estimate already covers ingestion and 30-day retention; no cost
increase results from the four-template least-privilege split.
