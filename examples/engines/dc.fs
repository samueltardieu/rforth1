\ Commande de moteurs
\ AREABOT

needs lib/tty.fs
needs examples/engines/dc-card-setup.fs
needs examples/engines/dc-vars.fs
needs examples/engines/can-engines.fs

-7     constant c/duty1
-6     constant angle/duty2
-2500   constant minduty2
-25000 constant period

: 8* 2* 4* ;

ADCON0 2 bit GO/DONE
: ad-init ( -- )
	0b10000001 ADCON0 c! 
	0b11000010 ADCON1 c!
;

\ Get the A/D value
: read-ad ( -- result )
	CHS0 bit-clr? var-set-1? = if 
		GO/DONE bit-set begin GO/DONE bit-clr? until
		ADRESL @ 
		CHS0 bit-toggle
		exit then
	prev_ad @
;

: raz-pid-vars 0 dre ! 0 ine ! 0 command ! ; inline
: back? back bit-set? ;
: working? working bit-set? ; inline
: working-on  working bit-set raz-pid-vars done-flag bit-clr ; inline
: working-off working bit-clr 0 v ! 0 command !  0 duty ! ; inline

: set-temp-values 
	acc-temp c@ acc c!
	dec-temp c@ dec c!
	vmax-temp @ vmax !
	tvmax-temp @ tvmax !
;

: stop 0 acc c! 0 dec c! 0 vmax c! 0 tvmax ! 0 v ! ;
	
: handle-double-start read-payload-1 working-on set-temp-values ; inline

: handle-start 
." Received start" cr 
engine1 bit-set? if set-1 handle-double-start then 
engine2 bit-set? if set-2 handle-double-start then 
;

: handle-double-stop  read-payload-1 stop ; inline
: handle-stop  
." Received stop" cr
engine1 bit-set? if set-1 handle-double-stop then 
engine2 bit-set? if set-2 handle-double-stop then 
; 

: handle-double-free  read-payload-1 working-off ; inline
: handle-free  
." Received free" cr 
engine1 bit-set? if set-1 handle-double-free then 
engine2 bit-set? if set-2 handle-double-free then 
; inline

: handle-double-forth  read-payload-6 back bit-clr ; inline
: handle-forth ." Received forth" cr 
engine1 bit-set? if set-1 handle-double-forth then 
engine2 bit-set? if set-2 handle-double-forth then 
; inline


: handle-double-back  read-payload-6 back bit-set ; inline
: handle-back  ." Received back" cr 
engine1 bit-set? if set-1 handle-double-back then 
engine2 bit-set? if set-2 handle-double-back then 
; inline



: wait-timer0 ( -- ) TMR0L ! TMR0IF bit-clr begin TMR0IF bit-set? until  ;

: integrate-pos p_theo @ v @  back? if - else + then  p_theo ! ;
: update-dre error @ prev_error @ - dre ! error @ prev_error ! ;
: update-ine error @ ine +! ;
: update-error p_theo @ p_mesu @ - error ! ;
: ponderate-errors ( -- n )
	error @  kp @ 100 */ 
	ine   @  ki @ 100 */ +
	dre   @  kd @ 100 */ +
;

: set-command 
ponderate-errors 
dup command ! 
0< if command @ negate command ! DIR bit-set else DIR bit-clr then 
command @ 10000 > if 10000 command ! then 
;

: pid ( -- )
	update-error
	update-ine
	update-dre
	set-command
;

: delta-ad ( -- delta ) 
	read-ad dup prev_ad @ - swap prev_ad ! 
	dup -256 < if negate 512 - else dup 256 >= if negate 512 + then then  
;

: scale ( delta -- delta' ) 1 16 */ ; 
: update-p_mesu delta-ad scale  p_mesu +! ; 
: calculate-duty  command @ c/duty1 * duty ! ;
: do-pwm duty @ 0<> if PWM bit-set duty @ wait-timer0 PWM bit-clr then ;

: vmax-reached? ( -- f) vmax @ v @ <= ;
: tvmax-not-elapsed? ( -- f) tvmax @ 0> ;
: accelerate? ( -- f ) vmax-reached? 0= tvmax-not-elapsed? and ;
: constant-speed? ( -- f) vmax-reached? tvmax-not-elapsed? and ;
: accelerate  acc c@ v +! ;
: deccelerate dec c@ v -! ;
: dec-tvmax 1 tvmax -! ;
: constant-speed  dec-tvmax vmax @ v ! ;
: update-mtimer mtimer c@ dup 20 > if mtimer-reached bit-set drop 0 else mtimer-reached bit-clr 1+ then mtimer c! ;    

: mtimer? ( -- f) mtimer-reached bit-set? ;

: update-speed
accelerate?     if accelerate   exit then
constant-speed? if constant-speed exit then 
v @ dec c@ 	>	if deccelerate exit then
0 v ! 
done-flag bit-clr? if send-done done-flag bit-set then
;

: update-values
working? 0= if exit then   
mtimer? if ( update-speed )  integrate-pos pid then
calculate-duty update-p_mesu
;

: timer1-reset period TMR1L ! TMR1IF bit-clr ; ( 200 Hz )
: wait-for-timer1 ( -- ) begin TMR1IF bit-set? until ;


: control 
timer1-reset
set-1 do-pwm set-2 do-pwm 
update-mtimer
set-1 update-values 
set-2 update-values
handle-can led-toggle 
wait-for-timer1
;

: init-timers
\ timer0  ( 1:4 prescaler )
0b10000001 T0CON c!
\ timer1  ( 1:8 prescaler -> 50hz )
0b10110001 T1CON c! 
;

: set-pid
	." kp kd ki" cr
	read16 dup . kp ! cr
	read16 dup . kd ! cr
	read16 dup . ki ! cr
;

: copy-pid-eeprom
   kp @ eekp !
   ki @ eeki !
   kd @ eekd !
;

: load-pid-eeprom
	eekp @ kp !
	eeki @ ki !
	eekd @ kd !
;

: init-double-variables
	0 prev_ad !
	0 p_theo !
	working-on
;

: init-variables
	set-1 init-double-variables
	set-2 init-double-variables
	load-pid-eeprom
;  
: greetings ." Welcome to dc engines control" cr ;
: main ( -- ) greetings init-common can-setup ad-init init-variables init-timers begin  control  again ;


: debug
cr
." v=" v @ . cr 
." c=" command @ . cr
." kp=" kp @ . cr
." ki=" ki @ . cr
." kd=" kd @ . cr
." tvmax=" tvmax @ . cr
." p_theo=" p_theo @ . cr
." p_mesu=" p_mesu @ . cr
." dre=" dre @ . cr
." ine=" ine @ . cr
." d1=" duty @ . cr 
." error=" error @ . cr 
." p_error=" prev_error @ . cr 
." STK=" .s cr
." working=" working? negate . cr 
." back=" back? negate . cr
." acc
;


: handle-key 
        key 
		dup [char] z = if drop print-addresses exit then
		dup [char] x = if drop write-addresses exit then	
        dup [char] q = if drop copy-pid-eeprom ." Calibration wrote in Eeprom" cr exit then
        dup [char] w = if drop load-pid-eeprom ." Calibration loaded from Eeprom" cr exit then
        dup [char] a = if drop can-abort ." Can abort" cr exit then
        dup [char] c = if drop ." TXB0CON=" TXB0CON c@ . cr then
        dup [CHAR] d = if drop ." UN" set-1 debug  exit then 
        dup [CHAR] f = if drop ." DEUX" set-2 debug  exit then 
        dup [CHAR] j = if drop ." pos1 =" prev_ad @ . cr ." pos2 =" prev_ad @ . cr exit then 
        dup [CHAR] l = if drop ." Loopback on" cr can-loopback exit then 
        dup [CHAR] k = if drop ." Loopback off" cr can-normal exit then 
        dup [CHAR] p = if drop set-pid  exit then 
        dup [CHAR] 1 = if drop send-stop  exit then 
        dup [CHAR] 2 = if drop send-forth exit then
        dup [CHAR] 3 = if drop send-back exit then
        dup [CHAR] 8 = if drop set-1 1 v +! exit then
        dup [CHAR] 9 = if drop set-1 1 v -! exit then
       	[CHAR] 4 = if  send-start  exit then   
		depth . cr \ if you press any other key you get the stack size 
;
