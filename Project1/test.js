/*
class MyClass {
    *createIterator(){
        yield 1;
        yield 2;
        yield 3;
    }
}

let instance = new MyClass();
let iterator = instance.createIterator();
let result = iterator.next();
*/
let str = "Hello";

// Делает то же, что и
// for (var letter of str) alert(letter);

for (var l in str){
    console.log(str[l]);
}

let iterator = str[Symbol.iterator]();


while(true) {
    let result = iterator.next();
    if (result.done) break;
    console.log(result.value); // Выведет все буквы по очереди
  }