helm repo add elastic https://helm.elastic.co
helm repo update
helm install -f elasticsearch-values.yaml elasticsearch elastic/elasticsearch
helm install -f kibana-values.yaml kibana elastic/kibana