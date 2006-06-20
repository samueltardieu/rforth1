create can-msg
cvariable can-msg-0
cvariable can-msg-1
cvariable can-msg-2
cvariable can-msg-3
cvariable can-msg-4
cvariable can-msg-5
cvariable can-msg-6
cvariable can-msg-7
cvariable can-msg-length
cvariable can-msg-flags
can-msg-flags 0 bit can-msg-rtr         \ remote transmit request
cvariable can-buffer
cvariable can-offset                    \ computed offset for TX can-buffer
variable can-arbitration
cvariable can-flags
can-flags 0 bit can-use-monobuffer

: 4* 2* 2* ;
: 16* 4* 4* ;
: 5<< 2* 16* ;

: 4/ 2/ 2/ ;
: 16/ 4/ 4/ ;
: 5>> 2/ 16/ ;

: can-buffer0-full? ( -- f ) RXB0RXFUL bit-set? ; inline
: can-buffer1-full? ( -- f ) RXB1RXFUL bit-set? ; inline
: can-msg-present? ( -- f ) can-buffer0-full? can-buffer1-full? or ;

: can-receive-buffer0 ( -- )
  RXB0DLC c@ 0x0f and dup can-msg-length c!
  if RXB0D0 can-msg can-msg-length c@ memcpy then
  0 can-msg-flags c!
  RXB0RXRTR bit-set? if can-msg-rtr bit-set then
  RXB0SIDL c@ RXB0SIDH c@ 2>1 5>> can-arbitration !
  RXB0RXFUL bit-clr 
;

: can-receive-buffer1 ( -- )
  RXB1DLC c@ 0x0f and dup can-msg-length c!
  if RXB1D0 can-msg can-msg-length c@ memcpy then
  0 can-msg-flags c!
  RXB1RXRTR bit-set? if can-msg-rtr bit-set then
  RXB1SIDL c@ RXB1SIDH c@ 2>1 5>> can-arbitration !
  RXB1RXFUL bit-clr 
;

: can-receive ( -- )
  begin
    can-buffer0-full? if can-receive-buffer0 exit then
    can-buffer1-full? if can-receive-buffer1 exit then
  again
;

: compute-can-offset ( -- ) can-buffer c@ 16* can-offset c! ;

: tx0>txn ( addr -- addr' ) can-offset c@ - ;

: m0>mn ( addr -- addr' ) can-buffer c@ 4* + ;

: can-set-buffer ( n -- ) can-buffer c! compute-can-offset ;

: can-free-txbuffer? ( -- f )
  TXB0TXREQ bit-clr? if -1 exit then
  can-use-monobuffer bit-set? if 0 exit then
  TXB1TXREQ bit-clr? if -1 exit then
  TXB2TXREQ bit-clr? if -1 exit then
  0
;

: can-choose-buffer ( -- )
  begin
    TXB0TXREQ bit-clr? if 0 can-set-buffer exit then
    can-use-monobuffer bit-clr? if
      TXB1TXREQ bit-clr? if 1 can-set-buffer exit then
      TXB2TXREQ bit-clr? if 2 can-set-buffer exit then
    then
  again
;

: can-prepare-buffer ( -- )
  can-choose-buffer
  can-msg-length c@ TXB0DLC tx0>txn c!
  can-msg-length c@ if can-msg TXB0D0 tx0>txn can-msg-length c@ memcpy then
  can-arbitration @ 5<< 1>2 TXB0SIDH tx0>txn c! TXB0SIDL tx0>txn c!
;

: can-transmit-buffer ( -- ) TXB0CON tx0>txn 3 bit-set ;

: can-set-rtr ( n -- )  TXB0DLC tx0>txn 6 bit-set ;
: can-clr-rtr ( n -- )  TXB0DLC tx0>txn 6 bit-clr ;

: can-transmit ( -- ) can-prepare-buffer can-clr-rtr can-transmit-buffer ;

: can-transmit-rtr ( -- ) can-prepare-buffer can-set-rtr can-transmit-buffer ;

: can-mode ( mask -- )
  5<<
  dup CANCON c@ 0b00011111 and or CANCON c!
  begin dup CANSTAT c@ 0b11100000 and = until
  drop
;

: can-multibuffer ( -- ) can-use-monobuffer bit-clr ;
: can-monobuffer ( -- ) can-use-monobuffer bit-set ;

: can-config ( -- ) 0b100 can-mode ;
: can-normal ( -- ) 0b000 can-mode ;
: can-loopback ( -- ) 0b010 can-mode ;
: can-disable ( -- ) 0b001 can-mode ;
: can-listen-only ( -- ) 0b011 can-mode ;

\ can-set-filter, can-set-mask and can-disable-all-filters must be called
\ while in config mode only

: can-set-filter ( filter nfilter -- )
  w> can-buffer c! 5<< 1>2 RXF0SIDH m0>mn c! RXF0SIDL m0>mn c! ; inw

: can-set-mask ( mask nmask -- )
  w> can-buffer c! 5<< 1>2 RXM0SIDH m0>mn c! RXM0SIDL m0>mn c! ; inw

: can-disable-all-filters ( -- )
  0x7ff 0 can-set-mask 0x7ff 1 can-set-mask
  7 cfor 0x7ff cr@ 1- can-set-filter cnext ;

: can-init ( -- )
  TRISB 3 bit-set TRISB 2 bit-clr
  can-config
  ( 0x04 BRGCON1 c! ) ( 20MHz )
  0x09 BRGCON1 c! ( 40MHz )
  0x90 BRGCON2 c! 0x02 BRGCON3 c! 0x40 CIOCON c!
  can-disable-all-filters
  can-normal
;

: can-emit-rtr ( a -- )
  can-arbitration !
  0 can-msg-length c!
  can-transmit-rtr
;

: can-abort ( -- ) ABAT bit-set ABAT bit-clr ; inline

: can-emit-0 ( a -- )
  can-arbitration !
  0 can-msg-length c!
  can-transmit
;

: can-emit-1 ( u a -- )
  can-arbitration !
  can-msg-0 !
  2 can-msg-length c!
  can-transmit
;

: can-emit-2 ( u1 u2 a -- )
  can-arbitration !
  can-msg-2 !
  can-msg-0 !
  4 can-msg-length c!
  can-transmit
;

: can-emit-3 ( u1 u2 u3 a -- )
  can-arbitration !
  can-msg-4 !
  can-msg-2 !
  can-msg-0 !
  6 can-msg-length c!
  can-transmit
;

: can-emit-4 ( u1 u2 u3 u4 a -- )
  can-arbitration !
  can-msg-6 !
  can-msg-4 !
  can-msg-2 !
  can-msg-0 !
  8 can-msg-length c!
  can-transmit
;

: can-receive-0 ( -- a )
  can-receive
  can-arbitration @
;

: can-receive-1 ( -- u a )
  can-receive
  can-msg-0 @
  can-arbitration @
;

: can-receive-2 ( -- u1 u2 a )
  can-receive
  can-msg-0 @
  can-msg-2 @
  can-arbitration @
;

: can-receive-3 ( -- u1 u2 u3 a )
  can-receive
  can-msg-0 @
  can-msg-2 @
  can-msg-4 @
  can-arbitration @
;

: can-receive-4 ( -- u1 u2 u3 a )
  can-receive
  can-msg-0 @
  can-msg-2 @
  can-msg-4 @
  can-msg-6 @
  can-arbitration @
;
