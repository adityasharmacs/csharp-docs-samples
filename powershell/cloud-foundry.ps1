# From http://docs.pivotal.io/pivotalcf/1-8/customizing/gcp-prepare-env.html
gcloud compute networks create opsmgr --mode=custom
gcloud compute networks subnets create opsmgr-subnet --network=opsmgr --range=10.0.0.0/20
# New-GceFirewallProtocol tcp -Port 0..65535 | New-GceFirewallProtocol udp -Port 0..65535 | New-GceFirewallProtocol icmp | Add-GceFirewall all-internal -Network opsmgr -SourceRange 10.0.0.0/20
gcloud compute firewall-rules create all-internal '--allow=tcp:0-65535,udp:0-65535,icmp' --network=opsmgr --source-ranges=10.0.0.0/20
gcloud compute firewall-rules create pcf-opsmanager '--allow=tcp:22,tcp:80,tcp:443' --network=opsmgr --source-ranges=0.0.0.0/0 --target-tags=pcf-opsmanager
gcloud compute firewall-rules create pcf-lb '--allow=tcp:80,tcp:443,tcp:2222,tcp:8080' --network=opsmgr --source-ranges=0.0.0.0/0 --target-tags=pcf-lb
gcloud compute firewall-rules create pcf-tcp-lb '--allow=tcp:1024-65535' --network=opsmgr --source-ranges=0.0.0.0/0 --target-tags=pcf-tcp-router