FROM node:24-alpine AS build
WORKDIR /app
COPY src/SteamIRL.Web/package*.json ./
RUN npm ci
COPY src/SteamIRL.Web/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:stable-alpine
COPY src/SteamIRL.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
