\ 
\ Common can interface for dc and stepper engines code
\ AREABOT

forward handle-stop
forward handle-free
forward handle-back
forward handle-forth
forward handle-start
forward handle-key

\ masks
\ P<add><pri>
0b01111100000 constant addr_mask 
0b00000011111 constant private_mask 
0b00000000111 constant forth_mask
0b00000000110 constant back_mask
0b00000000000 constant free_mask
0b00000000100 constant stop_mask
0b00000001000 constant start_mask
0b00000001111 constant done_mask
\  010 0000 0000
\  2		0   0
\  010 0010 0000
\  2    2   0
\  010 0110 0000
\  2    6    0

cvariable can-my-flags
can-my-flags 0 bit engine1
can-my-flags 1 bit engine2


variable sender
cvariable opaque
variable local_addr
variable both_card_addr
variable send_to_addr

eevariable ee_local_addr
eevariable ee_both_card_addr
eevariable ee_send_to_addr

: read-addresses
	ee_local_addr @ local_addr !
	ee_both_card_addr @ both_card_addr !
	ee_send_to_addr @ send_to_addr !
;

: print-addresses
." local addr "	local_addr @ . cr
." bothcard addr " both_card_addr @ . cr
." sendto addr "	send_to_addr   @ . cr
;
: can-setup 
	read-addresses
    can-init
	can-config
    local_addr @ 0 can-set-filter
    both_card_addr @ 2 can-set-filter 
	addr_mask 0 can-set-mask
	addr_mask 1 can-set-mask
	can-normal
; 

: write-addresses
	." local_addr=" cr
	read16 dup . cr ee_local_addr ! 
	." both_card_addr=" cr
	read16 dup . cr ee_both_card_addr ! 
	." send_to_addr=" cr
	read16 dup . cr ee_send_to_addr !  
	read-addresses
	can-setup
	." Addresses updated"
	cr
	
;


\ reads the standard identifier
: read-identifier ( -- n ) can-receive can-arbitration @ dup addr_mask and dup sender ! ." sender=" . cr ;

\ reads the payload
: read-payload-6 ( -- ) 
  can-msg-0 c@ dup ." opaque=" . cr opaque c!
  can-msg-1 c@ dup ." acc=" . cr acc-temp c! 
  can-msg-2 c@ can-msg-3 c@ 2>1 dup ." vmax=" . cr vmax-temp !  
  can-msg-4 c@ dup ." tvmax=" . cr  tvmax-temp ! 
  can-msg-5 c@ dup ." dec=" . cr dec-temp c! 
;

: read-payload-1 ( -- )
  can-msg-0 c@ dup ." opaque=" . cr opaque c!
;  
  
\ send a can message of payload 6
: emit6 ( i d1 d16b d4 d5 -- )
  6 can-msg-length c!
  can-msg-5 c! can-msg-4 c! 1>2 can-msg-3 c! can-msg-2 c! can-msg-1 c!
  can-msg-0 c!
  can-arbitration !
  can-transmit
;

: emit1 ( i o -- )
  1 can-msg-length c!
  can-msg-0 c!
  can-arbitration !
  can-transmit
;

: send-done sender @ done_mask or opaque c@ emit1 ; inline

: send-stop send_to_addr @ stop_mask or 0xFF emit1 ." Sent stop" cr ; inline

: send-free send_to_addr @  free_mask or 0xFF emit1 ." Sent free" cr ; inline 

: send-start send_to_addr @ start_mask or 0xFF emit1 ." Sent start" cr ; inline

: send-forth
send_to_addr @ forth_mask or 0xFF read8 dup . cr read16 dup . cr 100 8 emit6 
." Sent forth" cr
; inline

: send-back
send_to_addr @ back_mask or  0xFF read8 dup . cr read16 dup . cr 100 8 emit6
." Sent back" cr
; inline

: set-can-flags 
	addr_mask and
	dup both_card_addr @ = if drop  0b00000011 can-my-flags c! exit then
	    local_addr @ = if      0b00000010 can-my-flags c! else
							 0b00000001 can-my-flags c! then
;

: handle-message
		." Message received" cr
		dup set-can-flags
		private_mask and \ we only test the private identifier 
		dup ." Command:" . cr
		dup forth_mask = if drop handle-forth exit then
		dup back_mask  = if drop handle-back exit then
		dup free_mask  = if drop handle-free exit then
		dup start_mask  = if drop handle-start exit then
		dup done_mask  = if drop ." Done received" cr exit then
			stop_mask  = if handle-stop exit then
		." Unknown message" cr
;

: handle-serial key? if handle-key then ;
: handle-can
	can-msg-present? if read-identifier handle-message then
	handle-serial
;
