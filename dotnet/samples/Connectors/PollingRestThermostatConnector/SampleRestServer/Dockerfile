# Dockerfile
FROM mcr.microsoft.com/azurelinux/base/nodejs:20

WORKDIR /usr/src/app
COPY package*.json ./
RUN npm install
COPY . .

EXPOSE 80
CMD ["node", "server.js"]