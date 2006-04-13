needs lib/tty.fs

: main ( -- )
  can-init
  can-config
  0x378 0 can-set-filter
  0x176 1 can-set-filter
  can-loopback
  .s cr
  ." Sending a message 1234 with arbitration 378" cr
  0x1234 0x378 can-emit-1
  .s cr
  ." Receiving message" cr
  can-receive-1
  .s cr
  ." Arbitration: " . cr
  ." RTR:         " can-msg-rtr bit-set? . cr
  ." Data:        " . cr
  ." Sending a RTR with arbitration 176" cr
  0x176 can-emit-rtr
  .s cr
  can-receive
  .s cr
  ." Arbitration: " can-arbitration @ . cr
  ." RTR:         " can-msg-rtr bit-set? . cr
;