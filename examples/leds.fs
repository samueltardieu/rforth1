needs lib/tty-rs232.fs

cvariable leds

: greetings ." Press a key to stop" cr ;

: init-timer 0x84 T0CON c! ;

: change-leds
  TMR0IF bit-clr 0b11110000 TRISA c!
  leds c@ 2/ dup LATA c! dup leds c!
  dup . cr
  1 = if 16 leds c! then ;

: play
  16 leds c!
  begin key? if exit then TMR0IF bit-set? if change-leds then again ;

: thankyou ." Thank you" cr ;

: bye ." Press another one" cr $ff LATA c! begin key? until thankyou ;

: main greetings init-timer play ." You pressed: " key emit cr bye ;
