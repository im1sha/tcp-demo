# tcp-demo
A simple application demonstrating TCP communication between a client and a server

## Implemented functionality
- Continuous polling of the server at a fixed interval.
- Numbering of messages.
- Stopping the client when it receives a special response from the server. After this response, the client stops the exchange and closes the connection.
- Stopping a connection attempt from the client if it has not yet connected to the server, but a user command to disconnect is received.
## Advanced features
- Server disconnect detection.
- Idle state detection.
- Event logging.
- Communication logging.
- State logging.
## User interface features
- Output a log of events, communications and connection states on the client and server sides.
- Displays the current state of the client and server.
- Ability to select client and server addresses.
- Adaptive interface.
## Not yet implemented functionality
- Automatic reconnection and continuation of messaging.
## Known issues
- Physical disconnection (Shutdown) is not yet used because it causes the next attempt to communicate with the same addresses to fail (only one usage of each socket address is normally permitted). Possible fix: use a SO_LINGER flag on the client side.
## Compatibility
- Requires .NET Framework 4.7.2 or higher.
