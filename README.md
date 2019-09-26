# CryptoTunnel

## Purpose

Map plain local port to remote ssl port. Can be used for security testing.

## Usage

`CryptoTunnel <listen_ip> <listen_port> <remote_hostname> <remote_port> <reread_timeout> [<certificate_path>]`

- `listen_ip` - local IP Address (`0.0.0.0` or local interface)
- `listen_port` - local TCP Port
- `remote_hostname` - domain name of target. IP address not enough cause domain name required during SSL connection establishing. (You can find it in certificate of target)
- `remote_port` - remote TCP Port
- `reread_timeout` - timeout in milleseconds for wait second message from server (see Motivation). Use 0 if you dont need. 300 works fine in my projects.
- `certificate_path` - path to client certificate. Certificate must be installed in your storage and private key must be attached. Skep if you dont need client certificate. 

## Motivation

Burp doesn't support crypto ciphers like GOST2012-GOST8912-GOST8912 and GOST2001-GOST89-GOST89. It is possible to test with Fiddler on Windows (it use same crypto). But, it is HTTP proxy. And it can make changes in your traffic, especially if you play with Content-Length and Transfer-Encoding headers. More if web server sended you two responses not in one "tcp-packet" Burp would show only first, so you can lose info.

## Requirements

For GOST ciphers you need to install [CryptoPro CSP](https://www.cryptopro.ru/products/csp).
