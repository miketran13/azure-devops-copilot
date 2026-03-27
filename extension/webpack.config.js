const path = require("path");
const webpack = require("webpack");
const CopyWebpackPlugin = require("copy-webpack-plugin");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");

const isDev = process.env.NODE_ENV !== "production";

/** @type {import('webpack').Configuration} */
module.exports = {
  entry: {
    hub: "./src/Hub/Hub.tsx",
    "work-item-group": "./src/WorkItemGroup/WorkItemGroup.tsx",
    "context-menu": "./src/Actions/ContextMenuAction.tsx",
    "global-chat": "./src/GlobalChat/GlobalChat.tsx",
  },
  output: {
    filename: "[name]/[name].js",
    path: path.resolve(__dirname, "dist"),
    clean: !isDev, // don't clean in dev — devServer manages in-memory files
    devtoolModuleFilenameTemplate: "webpack:///[resource-path]",
  },
  // Dev server: serves the extension locally over HTTPS on port 3000
  // Azure DevOps loads extension iframes from baseUri (https://localhost:3000)
  // Run: npm run dev  — then accept the self-signed cert by visiting https://localhost:3000
  devServer: {
    server: "https",
    port: 3000,
    static: {
      directory: path.resolve(__dirname, "dist"),
    },
    allowedHosts: "all",
    headers: {
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, HEAD, OPTIONS",
      "Access-Control-Allow-Headers": "*",
      // Chrome Private Network Access (PNA): required so Azure DevOps (public)
      // can load iframes / fetch from localhost (private network) during local dev.
      "Access-Control-Allow-Private-Network": "true",
    },
    // Handle OPTIONS preflight requests explicitly for PNA compliance
    setupMiddlewares(middlewares, devServer) {
      devServer.app.use((req, res, next) => {
        if (req.method === "OPTIONS") {
          res.setHeader("Access-Control-Allow-Origin", "*");
          res.setHeader("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
          res.setHeader("Access-Control-Allow-Headers", "*");
          res.setHeader("Access-Control-Allow-Private-Network", "true");
          res.status(204).end();
          return;
        }
        next();
      });
      return middlewares;
    },
    hot: false,
    liveReload: false,
    devMiddleware: {
      writeToDisk: true, // write rebuilt files to dist/ so packaging still works
    },
  },
  resolve: {
    extensions: [".ts", ".tsx", ".js", ".jsx"],
    alias: {
      "@": path.resolve(__dirname, "src"),
    },
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: "ts-loader",
        exclude: /node_modules/,
      },
      {
        test: /\.scss$/,
        use: [
          MiniCssExtractPlugin.loader,
          "css-loader",
          { loader: "sass-loader", options: { api: "modern" } },
        ],
      },
      {
        test: /\.css$/,
        use: [
          MiniCssExtractPlugin.loader,
          "css-loader",
        ],
      },
      {
        test: /\.(png|svg|jpg|gif|woff|woff2|eot|ttf)$/,
        type: "asset/resource",
      },
    ],
  },
  plugins: [
    new webpack.DefinePlugin({
      // Injected at build time. Set BACKEND_URL env var in CI to override.
      // Falls back to localhost for local dev.
      "process.env.BACKEND_URL": JSON.stringify(
        process.env.BACKEND_URL || "http://localhost:7071/api"
      ),
    }),
    new MiniCssExtractPlugin({
      filename: "[name]/[name].css",
    }),
    new CopyWebpackPlugin({
      patterns: [
        { from: "src/Hub/Hub.html", to: "hub/hub.html" },
        { from: "src/WorkItemGroup/WorkItemGroup.html", to: "work-item-group/work-item-group.html" },
        { from: "src/Actions/ContextMenuAction.html", to: "context-menu/context-menu.html" },
        { from: "src/GlobalChat/GlobalChat.html", to: "global-chat/global-chat.html" },
        { from: "static/", to: "static/", noErrorOnMissing: true },
        { from: "azure-devops-extension.json", to: "azure-devops-extension.json" },
        { from: "overview.md", to: "overview.md" },
      ],
    }),
  ],
  devtool: "source-map",
  stats: {
    warnings: false,
  },
};
