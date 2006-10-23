variable a
: foo 1 2 + drop ;
: test1 0 cfor foo cnext ;
: test2 1 0 cfor foo cnext drop ;
: test3 8 cfor foo cnext ;
: test4 1 8 cfor foo cnext drop ;
: test5 cfor foo cnext ;
: test6 cfor foo cnext drop ;
: test7 a c@ cfor foo cnext ;
: test8 a c@ cfor foo cnext drop ;
: main test1 test2 test3 test4 8 test5 1 8 test6 test7 test8 ;
