# Palace Pal

Shows possible trap & hoard coffer locations in Palace of the Dead & Heaven on High. 

## Installation

To install this plugin from my plugin repository, please check the
[Installation Instructions](https://github.com/carvelli/Dalamud-Plugins#installation).

Additionally, you **need to install Splatoon**, which is used to render the visible overlays.
Please check [Splatoon's Installation Instructions](https://github.com/NightmareXIV/MyDalamudPlugins#installation).

## Server Installation

To run your own server, compile this plugin in DEBUG mode, load it as a dev plugin and configure the server as follows:

```sh
# create the directory for the sqlite db & some keys
mkdir data

# generate a random key (don't need to use openssl, any other base64 string is fine)
openssl rand -base64 48 > data/jwt.key

# start the server
docker run -it --rm -v "$(pwd)/data:/data" -p 127.0.0.1:5415:5415 ghcr.io/carvelli/palace-pal
```
