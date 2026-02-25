server-wanted           [udp broadcast] client asks for available testing server
server-available        [udp direct reply] server returns tcp host and port
client-hello            [tcp] client identity + client version
server-hello            [tcp] server accepts client and issues session token
test-begin              [tcp] testing session started
ping                    [tcp] heartbeat ping from server
pong                    [tcp] heartbeat pong from client
testgroup-start         [tcp] start of a testing group
testcase                [tcp] server sends testcase input payload
testcase-solved         [tcp] client sends computed output payload
testcase-result         [tcp] server sends Passed/Failed result
testgroup-end           [tcp] end of testing group
test-end                [tcp] end of full test run
stop                    [tcp] session cancelled by client or server
error                   [tcp] protocol or internal server error
