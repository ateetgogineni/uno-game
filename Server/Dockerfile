FROM node:22-alpine
WORKDIR /app
COPY package.json ./
COPY src ./src
ENV NODE_ENV=production PORT=8090
EXPOSE 8090
USER node
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8090/health || exit 1
CMD ["node", "src/index.js"]
