
cvariable flags
flags 0 bit back1
flags 1 bit back2
flags 2 bit working1
flags 3 bit working2
flags 4 bit mtimer-reached
flags 5 bit done-flag1
flags 6 bit done-flag2
flags 7 bit var-set-1

: var-set-1? var-set-1 bit-set? ;
: set-1  var-set-1 bit-set ;
: set-2  var-set-1 bit-clr ;
: back var-set-1? if back1 else back2 then ;
: working var-set-1? if working1 else working2 then ;
: done-flag var-set-1? if done-flag1 else done-flag2 then ;

: PWM var-set-1? if PWM1 else PWM2 then ;
: DIR var-set-1? if DIR1 else DIR2 then ;

variable v1
variable v2
: v var-set-1? if v1 else v2 then ;
variable duty1
variable duty2
: duty var-set-1? if duty1 else duty2 then ;
variable p_theo1
variable p_theo2
: p_theo var-set-1? if p_theo1 else p_theo2 then ;
variable p_mesu1
variable p_mesu2
: p_mesu var-set-1? if p_mesu1 else p_mesu2 then ;
variable prev_ad1
variable prev_ad2
: prev_ad var-set-1? if prev_ad1 else prev_ad2 then ;
variable command1
variable command2
: command var-set-1? if command1 else command2 then ;
variable ine1
variable ine2
: ine var-set-1? if ine1 else ine2 then ;
variable dre1
variable dre2
: dre var-set-1? if dre1 else dre2 then ;
variable error1
variable error2
: error var-set-1? if error1 else error2 then ;
variable prev_error1
variable prev_error2
: prev_error var-set-1? if prev_error1 else prev_error2 then ;

variable vmax1
variable vmax2
: vmax var-set-1? if vmax1 else vmax2 then ;
variable vmax-temp1
variable vmax-temp2
: vmax-temp var-set-1? if vmax-temp1 else vmax-temp2 then ;
variable tvmax1
variable tvmax2
: tvmax var-set-1? if tvmax1 else tvmax2 then ;
variable tvmax-temp1
variable tvmax-temp2
: tvmax-temp var-set-1? if tvmax-temp1 else tvmax-temp2 then ;
cvariable acc1
cvariable acc2
: acc var-set-1? if acc1 else acc2 then ;
cvariable acc-temp1
cvariable acc-temp2
: acc-temp var-set-1? if acc-temp1 else acc-temp2 then ;
cvariable dec1
cvariable dec2
: dec var-set-1? if dec1 else dec2 then ;
cvariable dec-temp1
cvariable dec-temp2
: dec-temp var-set-1? if dec-temp1 else dec-temp2 then ;

variable kp
variable ki
variable kd
cvariable mtimer
eevariable eekp 
eevariable eeki 
eevariable eekd
