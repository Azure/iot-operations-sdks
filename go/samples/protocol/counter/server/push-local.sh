CGO_ENABLED=0 go build -o server main.go
docker build -t localhost:5500/counter-server-go .
docker push localhost:5500/counter-server-go:latest
kubectl delete -f deployment.yaml 
kubectl apply -f deployment.yaml 
