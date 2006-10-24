\ rainbow: display all colors through a tri-color led connected as follow:
\  - port B5: blue color
\  - port B6: green color
\  - port B7: red color
\ A color is displayed if set output is set to low.
\
\ This program works by using a PWM to vary each color intensity. The
\ saturation and luminance are set to their maximum (252).

needs lib/tty-rs232.fs
needs lib/hsl-to-rgb.fs

cvariable leds    \ This variable is used to latch changes made to the leds

leds 5 bit led-blue
leds 6 bit led-green
leds 7 bit led-red

cvariable ctr
cvariable ctg
cvariable ctb

\ Apply changes present in the latch to the physical output
: apply ( -- ) leds c@ LATB c! ;

\ Turn all leds on
: leds-on ( -- ) 0 leds c! ;

\ Given a color, transform it into a counter (as cfor/cnext counts in
\ reverse direction).
: transform ( color -- counter ) 252 - negate ;

\ Given a hue, store the corresponding counter values into ctb/ctg/ctr
: set-color ( h -- )
  252 252 hsl-to-rgb transform ctb c! transform ctg c! transform ctr c!
;

\ Display one PWM cycle for a given hue
: color-cycle ( h -- )
  set-color leds-on
  252 cfor
    ci ctr c@ = if led-red bit-set then
    ci ctg c@ = if led-green bit-set then
    ci ctb c@ = if led-blue bit-set then
    apply
  cnext
;

\ Display one full rainbow cycle
: rainbow-cycle ( -- ) 252 cfor ci color-cycle cnext ;

\ Display rainbow colors until a key is pressed
: main ( -- ) begin rainbow-cycle key? until key drop ;
