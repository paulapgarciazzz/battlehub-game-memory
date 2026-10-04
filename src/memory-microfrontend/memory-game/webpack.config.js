/* eslint-disable @typescript-eslint/no-require-imports */
const path = require('path');
const webpack = require('webpack');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const TerserPlugin = require('terser-webpack-plugin');
const { BundleAnalyzerPlugin } = require('webpack-bundle-analyzer');
const { ModuleFederationPlugin } = webpack.container;
const sharedDeps = require('./mf-shared');

// Puerto del dev server. El backend solo acepta CORS desde este origen
// (modo independiente) y desde el Shell (http://localhost:4000).
const DEV_PORT = 8080;

module.exports = function (env, { analyze }) {
  const production = env.production || process.env.NODE_ENV === 'production';

  return {
    target: 'web',
    mode: production ? 'production' : 'development',
    devtool: production ? undefined : 'eval-source-map',
    optimization: {
      minimizer: [
        new TerserPlugin({
          // Modo rápido de Terser (igual que el Shell): sin compress.
          terserOptions: { compress: false }
        })
      ]
    },
    entry: {
      entry: './src/main.ts'
    },
    output: {
      clean: true,
      path: path.resolve(__dirname, 'dist'),
      filename: production ? '[name].[contenthash].bundle.js' : '[name].bundle.js',
      // 'auto': los chunks e imágenes se piden al origen del remote, aunque
      // el código se ejecute dentro del Shell.
      publicPath: 'auto',
      uniqueName: 'memoryGame'
    },
    resolve: {
      extensions: ['.ts', '.js'],
      modules: [path.resolve(__dirname, 'src'), 'node_modules']
    },
    devServer: {
      historyApiFallback: true,
      open: false,
      port: DEV_PORT,
      // El Shell descarga remoteEntry.js desde otro origen.
      headers: { 'Access-Control-Allow-Origin': '*' }
    },
    module: {
      rules: [
        { test: /\.(png|svg|jpg|jpeg|gif|webp)$/i, type: 'asset' },
        { test: /\.(woff|woff2|ttf|eot|otf)(\?v=[0-9]\.[0-9]\.[0-9])?$/i, type: 'asset' },
        { test: /\.css$/i, use: ['style-loader', 'css-loader'] },
        { test: /\.ts$/i, use: ['ts-loader', '@aurelia/webpack-loader'], exclude: /node_modules/ },
        {
          test: /[/\\]src[/\\].+\.html$/i,
          use: '@aurelia/webpack-loader',
          exclude: /node_modules/
        }
      ]
    },
    plugins: [
      new ModuleFederationPlugin({
        name: 'memoryGame',
        filename: 'remoteEntry.js',
        shared: sharedDeps
      }),
      // En producción se usa config/environment.production.json.
      production && new webpack.NormalModuleReplacementPlugin(
        /config[/\\]environment\.json$/,
        resource => {
          resource.request = resource.request.replace(/environment\.json$/, 'environment.production.json');
        }
      ),
      new HtmlWebpackPlugin({ template: 'index.html' }),
      new CopyWebpackPlugin({
        patterns: [{ from: 'static', to: '.', globOptions: { ignore: ['**/.*'] } }]
      }),
      analyze && new BundleAnalyzerPlugin()
    ].filter(p => p)
  };
};
